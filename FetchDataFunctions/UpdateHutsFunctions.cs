using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FetchDataFunctions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions;

public class UpdateHutsFunctions(
    IHttpClientFactory clientFactory,
    IDbContextFactory<AlpinehutsDbContext> dbContextFactory,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<UpdateHutsFunctions> logger)
{
    private int MaxHutId =>
        int.TryParse(configuration["MAX_HUT_ID"], out var value) ? value : 750;

    [Function(nameof(UpdateHutsTimerTriggered))]
    public async Task UpdateHutsTimerTriggered(
        [TimerTrigger("%HutsUpdateSchedule%", RunOnStartup = false)]
        TimerInfo myTimer,
        [DurableClient] DurableTaskClient starter)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return;
        }

        // Start hutId should start at least with 1, not 0, that's why we add 1
        int startHutId = (int)timeProvider.GetUtcNow().DayOfWeek + 1;
        string instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(UpdateHutsOrchestrator), startHutId);
        logger.LogInformation("UpdateHut orchestrator started. Instance ID={InstanceId}", instanceId);
    }

    /// <summary>
    /// This function can update a single hut. More for debugging than actual live use.
    /// </summary>
    [Function(nameof(UpdateHutHttpTriggered))]
    public async Task<IActionResult> UpdateHutHttpTriggered(
        [HttpTrigger(AuthorizationLevel.Function, "get")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Update Hut HTTP trigger function received a request");

        string? hutIds = req.Query["hutid"];
        if (string.IsNullOrEmpty(hutIds))
        {
            return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
        }

        var result = new List<Hut?>();

        if ("all".Equals(hutIds, StringComparison.OrdinalIgnoreCase))
        {
            hutIds = string.Join(",", Enumerable.Range(1, MaxHutId));
        }

        var tasks = new List<Task<Hut?>>();

        foreach (var hutId in hutIds.Split(','))
        {
            if (!int.TryParse(hutId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId))
            {
                logger.LogWarning("Could not parse '{HutId}'. Ignoring", hutId);
                continue;
            }

            tasks.Add(GetHutFromProviderActivity(parsedId, cancellationToken));
        }

        var huts = await Task.WhenAll(tasks);
        result.AddRange(huts.Where(h => h != null));

        return new OkObjectResult(result);
    }

    [Function(nameof(UpdateHutsOrchestrator))]
    public async Task UpdateHutsOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var log = context.CreateReplaySafeLogger<UpdateHutsFunctions>();
        int startHutId = context.GetInput<int>();

        log.LogInformation("Starting orchestrator with startHutId={StartHutId}", startHutId);

        var tasks = new List<Task>();

        // Fan-out. Every day we check 1/7 of all hut IDs in the range
        for (int i = startHutId; i <= MaxHutId; i += 7)
        {
            tasks.Add(context.CallActivityAsync<Hut?>(nameof(GetHutFromProviderActivity), i));
        }

        // Fan in. Wait for all to be finished
        await Task.WhenAll(tasks);

        log.LogInformation("MaxHutId {MaxHutId} reached. Ending hut updating now.", MaxHutId);
    }

    [Function(nameof(GetHutFromProviderActivity))]
    public async Task<Hut?> GetHutFromProviderActivity([ActivityTrigger] int hutId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Executing {ActivityName} for hutid={HutId}", nameof(GetHutFromProviderActivity), hutId);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var existingHut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == hutId, cancellationToken);
            if (existingHut == null)
            {
                logger.LogInformation("No hut yet in the database for id={HutId}", hutId);
            }
            else
            {
                logger.LogDebug("Found existing hut for id={HutId} in the database. name={HutName}", hutId, existingHut.Name);
            }

            if (existingHut != null && existingHut.Source != "AV")
            {
                logger.LogInformation("Hut with ID={HutId} is not from Alpenverein source. Skipping update", hutId);
                return null;
            }

            var httpClient = clientFactory.CreateClient("HttpClient");

            var url = string.Format(CultureInfo.InvariantCulture, Helpers.GetHutInfosUrlV2, hutId);
            var hutInfoResponse = await httpClient.GetAsync(url, cancellationToken);

            // If 404, hut does not exist
            if (hutInfoResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                if ((await hutInfoResponse.Content.ReadAsStringAsync(cancellationToken)).Contains("Hut Id not found"))
                {
                    logger.LogInformation("Hut with ID={HutId} not found", hutId);
                    if (existingHut != null)
                    {
                        logger.LogInformation("Deleting hut with ID={HutId} from database", hutId);
                        dbContext.Huts.Remove(existingHut);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    return null;
                }
            }

            if (!hutInfoResponse.IsSuccessStatusCode)
            {
                logger.LogError("Error fetching hut info for ID={HutId}. StatusCode={StatusCode}", hutId, hutInfoResponse.StatusCode);
                return null;
            }

            var hutInfo = await hutInfoResponse.Content.ReadFromJsonAsync<HutInfoV2>(ScraperJson.Options, cancellationToken);
            if (hutInfo == null)
            {
                logger.LogError("Error deserializing hut info for ID={HutId}", hutId);
                return null;
            }

            if (Helpers.ExcludedHutNames.Contains(hutInfo.hutName))
            {
                logger.LogInformation("Skipping excluded hut name {HutName}", hutInfo.hutName);
                if (existingHut != null)
                {
                    logger.LogInformation("Deleting hut with ID={HutId} from database", hutId);
                    dbContext.Huts.Remove(existingHut);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                return null;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var hut = existingHut ?? new Hut();
            hut.Id = hutInfo.hutId;
            hut.Enabled = hutInfo.hutUnlocked;
            hut.Name = hutInfo.hutName;
            hut.HutWebsite = hutInfo.HutWebsiteNormalized;
            hut.Link = string.Format(CultureInfo.InvariantCulture, Helpers.HutBookingUrlV2, hutId);
            hut.Source = "AV"; // AV = Alpenverein, the source of the hut data
            hut.LastUpdated = now;
            hut.Added = existingHut?.Added ?? now;
            hut.Activated = existingHut?.Activated ?? (hutInfo.hutUnlocked ? now : null);

            if (existingHut?.ManuallyEdited == true)
            {
                logger.LogInformation("Hut with ID={HutId} was manually edited. Not updating location information", hutId);
            }
            else
            {
                hut.Country = hutInfo.CountryNormalized;
                hut.Altitude = hutInfo.AltitudeInt;
                hut.Longitude = hutInfo.Longitude;
                hut.Latitude = hutInfo.Latitude;

                if (hut.Latitude == null || hut.Longitude == null || !Helpers.CoordinatesSanityCheck(hut.Longitude.Value, hut.Latitude.Value))
                {
                    // Some Swiss huts come with coordinates in the Swiss national grid (CH1903/LV03) instead of
                    // WGS84. Try to convert those first before falling back to an online name-based search.
                    if (hut.Latitude != null && hut.Longitude != null &&
                        Helpers.TryConvertSwissGridToWgs84(hut.Latitude.Value, hut.Longitude.Value, out var swissLat, out var swissLon))
                    {
                        logger.LogInformation("Hut with ID={HutId} had Swiss national grid coordinates. Converted to WGS84 lat={Lat} long={Long}", hutId, swissLat, swissLon);
                        hut.Latitude = swissLat;
                        hut.Longitude = swissLon;
                    }
                    else
                    {
                        logger.LogInformation("Hut with ID={HutId} has no or unrealistic coordinates. Trying to look up hut online", hutId);
                        var coordinates = await Helpers.SearchHutCoordinates(hutInfo.hutName, httpClient, logger, cancellationToken);

                        hut.Latitude = coordinates.latitude ?? existingHut?.Latitude;
                        hut.Longitude = coordinates.longitude ?? existingHut?.Longitude;
                    }
                }

                if (hut is { Latitude: not null, Longitude: not null })
                {
                    var (country, region) = await Helpers.GetCountryAndRegion(hut.Latitude.Value, hut.Longitude.Value, logger, cancellationToken);
                    if (country != null)
                        hut.Country = country;
                    if (region != null)
                        hut.Region = region;
                }
            }

            if (existingHut == null)
                dbContext.Huts.Add(hut);

            await dbContext.SaveChangesAsync(cancellationToken);

            return hut;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in http call to provider: {Message}", e.Message);
        }

        return null;
    }
}
