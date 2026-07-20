using System.Data;
using System.Net.Http.Json;
using FetchDataFunctions.Functions;
using FetchDataFunctions.Models;
using FetchDataFunctions.Models.HuettenHoliday;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions.Functions.HuettenHoliday;

public class HuettenHolidayUpdateAvailabilityFromProvider(
    ILogger<HuettenHolidayUpdateAvailabilityFromProvider> logger,
    IHttpClientFactory clientFactory,
    IDbContextFactory<AlpinehutsDbContext> dbContextFactory,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider)
{
    private const int HutIdOffset = 10000; // Offset to avoid conflicts with other hut IDs

    [Function(nameof(HuettenHolidayUpdateAvailabilityHttpTriggered))]
    public async Task<IActionResult> HuettenHolidayUpdateAvailabilityHttpTriggered(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, string hutId, CancellationToken cancellationToken)
    {
        logger.LogInformation("HuettenHolidayUpdateAvailabilityHttpTriggered called with hutIds: {HutId}", hutId);

        var hutIdsList = hutId.Split(',').Select(i => int.Parse(i, System.Globalization.CultureInfo.InvariantCulture) + HutIdOffset).ToList();

        var availabilities = new List<Availability>();
        foreach (var hutIdInt in hutIdsList)
        {
            var availability = await HuettenHolidayUpdateAvailabilityActivityTrigger(hutIdInt, cancellationToken);
            if (availability != null)
            {
                availabilities.AddRange(availability);
            }
        }

        if (availabilities.Count == 0)
        {
            return new NotFoundObjectResult("No availability found.");
        }

        return new OkObjectResult(availabilities);
    }

    [Function(nameof(HuettenHolidayUpdateAvailabilityTimerTriggered))]
    public async Task HuettenHolidayUpdateAvailabilityTimerTriggered(
        [TimerTrigger("%HuettenHolidayAvailabilityUpdateSchedule%")] TimerInfo myTimer,
        [SqlInput("SELECT Id FROM [dbo].[Huts] WHERE Enabled = 1 and Source = 'HuettenHoliday'",
            "DatabaseConnectionString",
            CommandType.Text, "")]
        IEnumerable<Hut> huts,
        [DurableClient] DurableTaskClient starter)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return;
        }

        logger.LogInformation("{FunctionName} function executed", nameof(HuettenHolidayUpdateAvailabilityTimerTriggered));

        string instanceId =
            await starter.ScheduleNewOrchestrationInstanceAsync(nameof(HuettenHolidayUpdateAvailabilityOrchestrator), huts.Select(h => h.Id).ToList());
        logger.LogInformation("{OrchestratorName} started. Instance ID={InstanceId}", nameof(HuettenHolidayUpdateAvailabilityOrchestrator), instanceId);
    }

    [Function(nameof(HuettenHolidayUpdateAvailabilityOrchestrator))]
    public async Task HuettenHolidayUpdateAvailabilityOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var orchestratorLogger = context.CreateReplaySafeLogger<HuettenHolidayUpdateAvailabilityFromProvider>();

        var hutIds = context.GetInput<List<int>>();
        orchestratorLogger.LogInformation("Starting HuettenHoliday orchestrator with {Count} hut IDs", hutIds!.Count);

        await context.ProcessSequentiallyAsync<IEnumerable<Availability>?>(
            hutIds, nameof(HuettenHolidayUpdateAvailabilityActivityTrigger), orchestratorLogger);

        orchestratorLogger.LogInformation("HuettenHoliday Update availability orchestrator finished");
    }

    [Function(nameof(HuettenHolidayUpdateAvailabilityActivityTrigger))]
    public async Task<IEnumerable<Availability>?> HuettenHolidayUpdateAvailabilityActivityTrigger([ActivityTrigger] int hutId, CancellationToken cancellationToken)
    {
        try
        {
            var cabinId = hutId > HutIdOffset ? hutId - HutIdOffset : hutId;

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            // get hut from database to check if it exists
            var hut = await dbContext.Huts.Include(h => h.Availability)
                .ThenInclude(a => a.BedCategory)
                .AsNoTracking()
                .SingleOrDefaultAsync(h => h.Id == hutId, cancellationToken);

            if (hut == null)
            {
                logger.LogWarning("Hut with id {HutId} not found in database", hutId);
                return null;
            }

            var httpClient = clientFactory.CreateClient("HttpClient");

            // First, we need to make one GET call to huts booking page: https://www.huetten-holiday.com/huts/xxx
            // This returns two set-cookies that we need to use in the POST request:
            // XSRF-TOKEN
            // huettenholiday_session
            // Also XSRF-TOKEN from the cookie needs to be set in the header of the POST request
            logger.LogInformation("Fetching initial cookies for hutId {HutId}", hutId);
            var initialResponse = await httpClient.GetAsync(hut.Link, cancellationToken);
            if (!initialResponse.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch initial cookies from HuettenHoliday. Status code: {StatusCode}", initialResponse.StatusCode);
                return null;
            }

            // Extract cookies from the initial response
            var xsrfToken = initialResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal))?.Split(';').FirstOrDefault()?.Replace("XSRF-TOKEN=", "");
            var sessionCookie = initialResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.StartsWith("huettenholiday_session=", StringComparison.Ordinal))?.Split(';').FirstOrDefault()?.Replace("huettenholiday_session=", "");

            if (xsrfToken == null || sessionCookie == null)
            {
                logger.LogError("Failed to extract cookies from initial response for hutId {HutId}", hutId);
                return null;
            }

            var availabilities = new List<Availability>();
            const int monthsToFetch = 6; // Number of months to fetch availability for
            var now = timeProvider.GetUtcNow().UtcDateTime;

            for (var month = 0; month < monthsToFetch; month++)
            {
                var content = new GetMonthAvailabilityPayload
                {
                    cabinId = cabinId,
                    selectedMonth = new SelectedMonth
                    {
                        monthNumber = now.AddMonths(month).Month,
                        year = now.AddMonths(month).Year
                    }
                };
                logger.LogInformation("Fetching availability from HuettenHoliday for hutId {HutId} for month {Month}-{Year}", hutId, content.selectedMonth.monthNumber, content.selectedMonth.year);

                const string getMonthAvailabilityUrl = "https://www.huetten-holiday.com/cabins/get-month-availability";
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, getMonthAvailabilityUrl)
                {
                    Content = JsonContent.Create(content)
                };
                requestMessage.Headers.Add("X-XSRF-TOKEN", xsrfToken.Replace("%3D", "=")); // Replace URL encoded equals sign with actual equals sign
                requestMessage.Headers.Add("Cookie", $"XSRF-TOKEN={xsrfToken}; huettenholiday_session={sessionCookie}");

                var response = await httpClient.SendAsync(requestMessage, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to fetch availability from HuettenHoliday. Status code: {StatusCode}", response.StatusCode);
                    break;
                }

                var responseData = (await response.Content.ReadFromJsonAsync<IEnumerable<AvailabilityResult>>(ScraperJson.Options, cancellationToken))?.ToList();
                if (responseData == null || responseData.Count == 0)
                {
                    logger.LogInformation("No availability data found for hutId {HutId} in month {Month}-{Year}", hutId, content.selectedMonth.monthNumber, content.selectedMonth.year);
                    continue;
                }

                foreach (var dateResult in responseData)
                {
                    var existingAvailabilities = await dbContext.Availability.Where(a =>
                        a.Hutid == hutId && a.Date == dateResult.date &&
                        a.BedCategoryId != BedCategory.HutClosedBedcatoryId).ToListAsync(cancellationToken);

                    var existingAvailability = existingAvailabilities.FirstOrDefault();

                    // This is quite crude so far. Not sure, I fully understand the structure of the response yet. But it seems to be:
                    // Sum of booked_places gives the number of free beds
                    var totalFreeBeds = dateResult.rooms.Select(r => r.booked_places).Sum();
                    totalFreeBeds = totalFreeBeds < 0 ? 0 : totalFreeBeds; // Ensure we don't have negative free beds
                    var totalBeds = dateResult.totalPlaces;
                    if (existingAvailability == null)
                    {
                        logger.LogInformation("Creating new availability for hutId {HutId} on date {Date}", hutId, dateResult.date);
                        var availability = new Availability
                        {
                            Hutid = hutId,
                            Date = dateResult.date,
                            FreeRoom = totalFreeBeds,
                            TotalRoom = totalBeds,
                            LastUpdated = now,
                            BedCategoryId = 2, // Hardcoded to "Zimmer" for now
                            TenantBedCategoryId = 2,
                        };
                        dbContext.Availability.Add(availability);
                        availabilities.Add(availability);
                    }
                    else
                    {
                        logger.LogInformation("Updating existing availability for hutId {HutId} on date {Date}", hutId, dateResult.date);
                        existingAvailability.FreeRoom = totalFreeBeds;
                        existingAvailability.TotalRoom = totalBeds;
                        existingAvailability.LastUpdated = now;

                        dbContext.Availability.Update(existingAvailability);
                        availabilities.Add(existingAvailability);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation("Fetched {Count} availability records for hutId {HutId}", availabilities.Count, hutId);
            return availabilities;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while fetching availability from HuettenHoliday");
            return null;
        }
    }
}
