using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FetchDataFunctions.Models;
using FetchDataFunctions.Models.HuettenHoliday;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions.Functions.HuettenHoliday;

public class HuettenHolidayUpdateAvailabilityFromProvider
{
    private readonly ILogger<HuettenHolidayUpdateAvailabilityFromProvider> _logger;
    private readonly IHttpClientFactory _clientFactory;

    private const int HutIdOffset = 10000; // Offset to avoid conflicts with other hut IDs

    public HuettenHolidayUpdateAvailabilityFromProvider(ILogger<HuettenHolidayUpdateAvailabilityFromProvider> logger, IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _clientFactory = clientFactory;
    }

    [Function(nameof(HuettenHolidayUpdateAvailabilityHttpTriggered))]
    public async Task<IActionResult> HuettenHolidayUpdateAvailabilityHttpTriggered([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, string hutId)
    {
        _logger.LogInformation("HuettenHolidayUpdateAvailabilityHttpTriggered called with hutIds: {HutId}", hutId);

        var hutIdsList = hutId.Split(',').Select(i => int.Parse(i) + HutIdOffset).ToList();

        var availabilities = new List<Availability>();
        foreach (var hutIdInt in hutIdsList)
        {
            var availability = await HuettenHolidayUpdateAvailabilityActivityTrigger(hutIdInt);
            if (availability != null)
            {
                availabilities.AddRange(availability);
            }
        }

        // Get all huts from the provider, then filter by hutId
        if (availabilities.Count == 0)
        {
            return new NotFoundObjectResult("No availability found.");
        }

        return new OkObjectResult(availabilities);
    }
    
    [Function(nameof(HuettenHolidayUpdateAvailabilityTimerTriggered))]
        public async Task HuettenHolidayUpdateAvailabilityTimerTriggered(
            [TimerTrigger("0 0 13,22 * * *")] TimerInfo myTimer,
            [SqlInput("SELECT Id FROM [dbo].[Huts] WHERE Enabled = 1 and Source = 'HuettenHoliday'",
                "DatabaseConnectionString",
                CommandType.Text, "")]
            IEnumerable<Hut> huts,
            [DurableClient] DurableTaskClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }

            _logger.LogInformation($"{nameof(HuettenHolidayUpdateAvailabilityTimerTriggered)} function executed at: {DateTime.Now}");

            string instanceId =
                await starter.ScheduleNewOrchestrationInstanceAsync(nameof(HuettenHolidayUpdateAvailabilityOrchestrator), huts.Select(h => h.Id).ToList());
            _logger.LogInformation($"{nameof(HuettenHolidayUpdateAvailabilityOrchestrator)} started. Instance ID={instanceId}");
        }
        

        [Function(nameof(HuettenHolidayUpdateAvailabilityOrchestrator))]
        public async Task HuettenHolidayUpdateAvailabilityOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var orchestratorLogger = context.CreateReplaySafeLogger<UpdateAvailabilityFunctions>();

            var hutIds = context.GetInput<List<int>>();

            orchestratorLogger.LogInformation($"Starting HuettenHoliday orchestrator with {hutIds!.Count} hut IDs");

            var tasks = new List<Task>();

            // Fan-out
            foreach (var hutId in hutIds)
            {
                orchestratorLogger.LogInformation("Starting UpdateHutAvailability Activity Function for hutId={hutId}", hutId);
                tasks.Add(context.CallActivityAsync(nameof(HuettenHolidayUpdateAvailabilityActivityTrigger), hutId));

                // In order not to run into rate limiting, we process in batches of 10 and then wait for 1 minute
                if (tasks.Count >= 10)
                {
                    orchestratorLogger.LogInformation("Delaying next batch for 1 minute, last hutId={hutid}", hutId);
                    await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(1), CancellationToken.None);

                    orchestratorLogger.LogInformation("Waiting for batch to finishing UpdateHutAvailability Activity Functions");
                    // Fan-in (wait for all tasks to be completed)
                    await Task.WhenAll(tasks);
                    orchestratorLogger.LogInformation("Finished batch");

                    tasks.Clear();
                }
            }

            orchestratorLogger.LogInformation("All UpdateHutAvailability Activity Functions scheduled. Waiting for finishing last batch");

            // Fan-in (wait for all tasks to be completed)
            await Task.WhenAll(tasks);

            // Call stored proc to update reporting table
            //await context.CallActivityAsync(nameof(UpdateAvailabilityReporting), new object()); // using new object instead of null to satisfy analyzer warning

            orchestratorLogger.LogInformation($"HuettenHoliday Update availability orchestrator finished");
        }

    [Function(nameof(HuettenHolidayUpdateAvailabilityActivityTrigger))]
    public async Task<IEnumerable<Availability>?> HuettenHolidayUpdateAvailabilityActivityTrigger([ActivityTrigger] int hutId)
    {
        try
        {
            var cabinId = hutId > HutIdOffset ? hutId - HutIdOffset : hutId;

            var dbContext = Helpers.GetDbContext();
            // get hut from database to check if it exists
            var hut = await dbContext.Huts.Include(h => h.Availability)
                .ThenInclude(a => a.BedCategory)
                .AsNoTracking()
                .SingleOrDefaultAsync(h => h.Id == hutId);

            if (hut == null)
            {
                _logger.LogWarning("Hut with id {HutId} not found in database", hutId);
                return null;
            }

            var httpClient = _clientFactory.CreateClient("HttpClient");


            // First, we need to make one GET call to huts booking page: https://www.huetten-holiday.com/huts/xxx
            // This returns two set-cookies that we need to use in the POST request:
            // XSRF-TOKEN
            // huettenholiday_session
            // Also XSRF-TOKEN from the cookie needs to be set in the header of the POST request

            _logger.LogInformation("Fetching initial cookies for hutId {HutId}", hutId);
            var initialResponse = await httpClient.GetAsync(hut.Link);
            if (!initialResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch initial cookies from HuettenHoliday. Status code: {StatusCode}", initialResponse.StatusCode);
                return null;
            }

            // Extract cookies from the initial response
            var xsrfToken = initialResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.StartsWith("XSRF-TOKEN="))?.Split(';').FirstOrDefault()?.Replace("XSRF-TOKEN=", "");
            var sessionCookie = initialResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.StartsWith("huettenholiday_session="))?.Split(';').FirstOrDefault()?.Replace("huettenholiday_session=", "");

            if (xsrfToken == null || sessionCookie == null)
            {
                _logger.LogError("Failed to extract cookies from initial response for hutId {HutId}", hutId);
                return null;
            }

            var availabilities = new List<Availability>();
            const int monthsToFetch = 6; // Number of months to fetch availability for

            for (var month = 0; month < monthsToFetch; month++)
            {
                var content = new GetMonthAvailabilityPayload
                {
                    cabinId = cabinId,
                    selectedMonth = new SelectedMonth
                    {
                        monthNumber = DateTime.UtcNow.AddMonths(month).Month,
                        year = DateTime.UtcNow.AddMonths(month).Year
                    }
                };
                _logger.LogInformation("Fetching availability from HuettenHoliday for hutId {hutId} for month {Month}-{Year}", hutId, content.selectedMonth.monthNumber, content.selectedMonth.year);

                const string getMonthAvailabilityUrl = "https://www.huetten-holiday.com/cabins/get-month-availability";
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, getMonthAvailabilityUrl)
                {
                    Content = JsonContent.Create(content)
                };
                requestMessage.Headers.Add("X-XSRF-TOKEN", xsrfToken.Replace("%3D", "=")); // Replace URL encoded equals sign with actual equals sign
                requestMessage.Headers.Add("Cookie", $"XSRF-TOKEN={xsrfToken}; huettenholiday_session={sessionCookie}");

                var response = await httpClient.SendAsync(requestMessage);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch availability from HuettenHoliday. Status code: {StatusCode}", response.StatusCode);
                    break;
                }

                var responseData = (await response.Content.ReadFromJsonAsync<IEnumerable<AvailabilityResult>>())?.ToList();
                if (responseData == null || responseData.Count == 0)
                {
                    _logger.LogInformation("No availability data found for hutId {HutId}", hutId);
                    continue;
                }

                foreach (var dateResult in responseData)
                {
                    var existingAvailabilities = await dbContext.Availability.Where(a =>
                        a.Hutid == hutId && a.Date == dateResult.date &&
                        a.BedCategoryId != BedCategory.HutClosedBedcatoryId).ToListAsync();

                    var existingAvailability = existingAvailabilities.FirstOrDefault();

                    // This is quite crude so far. Not sure, I fully understand the structure of the response yet. But it seems to be:
                    // Sum of booked_places gives the number of free beds
                    var totalFreeBeds = dateResult.rooms.Select(r => r.booked_places).Sum();
                    var totalBeds = dateResult.totalPlaces;
                    if (existingAvailability == null)
                    {
                        _logger.LogInformation("Creating new availability for hutId {HutId} on date {Date}", hutId, dateResult.date);
                        var availability = new Availability
                        {
                            Hutid = hutId,
                            Date = dateResult.date,
                            FreeRoom = totalFreeBeds,
                            TotalRoom = totalBeds,
                            LastUpdated = DateTime.UtcNow,
                            BedCategoryId = 2, // Hardcoded to "Zimmer" for now
                            TenantBedCategoryId = 2,
                        };
                        dbContext.Availability.Add(availability);
                        availabilities.Add(availability);
                    }
                    else
                    {
                        _logger.LogInformation("Updating existing availability for hutId {HutId} on date {Date}", hutId, dateResult.date);
                        existingAvailability.FreeRoom = totalFreeBeds;
                        existingAvailability.TotalRoom = totalBeds;
                        existingAvailability.LastUpdated = DateTime.UtcNow;

                        dbContext.Availability.Update(existingAvailability);
                        availabilities.Add(existingAvailability);
                    }
                }

                await dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Fetched {Count} availability records for hutId {HutId}", availabilities.Count, hutId);
            return availabilities;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching huts from HuettenHoliday");
            return null;
        }
    }
}