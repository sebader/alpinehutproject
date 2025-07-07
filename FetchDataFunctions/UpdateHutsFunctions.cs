using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FetchDataFunctions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace FetchDataFunctions
{
    public class UpdateHutsFunctions
    {
        private readonly ILogger<UpdateHutsFunctions> _logger;

        private static int MaxHutId
        {
            get
            {
                if (int.TryParse(Environment.GetEnvironmentVariable("MAX_HUT_ID"), out int value))
                {
                    return value;
                }

                return 750;
            }
        }

        private readonly IHttpClientFactory _clientFactory;

        public UpdateHutsFunctions(IHttpClientFactory clientFactory, ILogger<UpdateHutsFunctions> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        [Function(nameof(UpdateHutsTimerTriggered))]
        public async Task UpdateHutsTimerTriggered(
            [TimerTrigger("0 0 2 * * *", RunOnStartup = false)]
            TimerInfo myTimer,
            [DurableClient] DurableTaskClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }

            // Start hutId should start at least with 1, not 0, thats why we add 1
            int startHutId = (int)DateTime.UtcNow.DayOfWeek + 1;
            string instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(UpdateHutsOrchestrator), startHutId);
            _logger.LogInformation($"UpdateHut orchestrator started. Instance ID={instanceId}");
        }

        /// <summary>
        /// This function can update a single hut. More for debugging than actual live use.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [Function(nameof(UpdateHutHttpTriggered))]
        public async Task<IActionResult> UpdateHutHttpTriggered(
            [HttpTrigger(AuthorizationLevel.Function, "get")]
            HttpRequest req)
        {
            _logger.LogInformation("Update Hut HTTP trigger function received a request");

            string hutIds = req.Query["hutid"];
            if (string.IsNullOrEmpty(hutIds))
            {
                return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
            }

            var result = new List<Hut?>();

            if ("all".Equals(hutIds, StringComparison.InvariantCultureIgnoreCase))
            {
                hutIds = string.Join(",", Enumerable.Range(1, MaxHutId));
            }

            var tasks = new List<Task<Hut?>>();

            foreach (var hutId in hutIds.Split(','))
            {
                if (!int.TryParse(hutId, out int parsedId))
                {
                    _logger.LogWarning("Could not parse '{hutId}'. Ignoring", hutId);
                }

                tasks.Add(GetHutFromProviderActivity(parsedId));
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

            log.LogInformation("Starting orchestrator with startHutId={startHutId}", startHutId);

            var tasks = new List<Task>();

            // Fan-out. Every day we check 1/7 of all hut IDs in the range
            for (int i = startHutId; i <= MaxHutId; i += 7)
            {
                tasks.Add(context.CallActivityAsync(nameof(GetHutFromProviderActivity), i));
            }

            // Fan in. Wait for all to be finished
            await Task.WhenAll(tasks);

            log.LogInformation("MaxHutId {MaxHutId} reached. Ending hut updating now.", MaxHutId);
        }

        [Function(nameof(GetHutFromProviderActivity))]
        public async Task<Hut?> GetHutFromProviderActivity([ActivityTrigger] int hutId)
        {
            try
            {
                _logger.LogInformation("Executing " + nameof(GetHutFromProviderActivity) + " for hutid={hutId}", hutId);
                var dbContext = Helpers.GetDbContext();

                var existingHut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == hutId);
                if (existingHut == null)
                {
                    _logger.LogInformation("No hut yet in the database for id={hutId}", hutId);
                }
                else
                {
                    _logger.LogDebug("Found existing hut for id={hutId} in the database. name={HutName}", hutId, existingHut.Name);
                }
                
                var httpClient = _clientFactory.CreateClient("HttpClient");

                var url = string.Format(Helpers.GetHutInfosUrlV2, hutId);
                var hutInfoResponse = await httpClient.GetAsync(url);

                // If 404, hut does not exist
                if (hutInfoResponse.StatusCode == HttpStatusCode.BadRequest)
                {
                    if ((await hutInfoResponse.Content.ReadAsStringAsync()).Contains("Hut Id not found"))
                    {
                        _logger.LogInformation("Hut with ID={hutId} not found", hutId);
                        if (existingHut != null)
                        {
                            _logger.LogInformation("Deleting hut with ID={hutId} from database", hutId);
                            dbContext.Huts.Remove(existingHut);
                            await dbContext.SaveChangesAsync();
                        }

                        return null;
                    }
                }

                if (!hutInfoResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Error fetching hut info for ID={hutId}. StatusCode={StatusCode}", hutId, hutInfoResponse.StatusCode);
                    return null;
                }

                var hutInfo = await hutInfoResponse.Content.ReadFromJsonAsync<HutInfoV2>();
                if (hutInfo == null)
                {
                    _logger.LogError("Error deserializing hut info for ID={hutId}", hutId);
                    return null;
                }

                if (Helpers.ExcludedHutNames.Contains(hutInfo.hutName))
                {
                    _logger.LogInformation("Skipping excluded hut name {hutName}", hutInfo.hutName);
                    if (existingHut != null)
                    {
                        _logger.LogInformation("Deleting hut with ID={hutId} from database", hutId);
                        dbContext.Huts.Remove(existingHut);
                        await dbContext.SaveChangesAsync();
                    }

                    return null;
                }

                var hut = existingHut ?? new Hut();
                hut.Id = hutInfo.hutId;
                hut.Enabled = hutInfo.hutUnlocked;
                hut.Name = hutInfo.hutName;
                hut.HutWebsite = hutInfo.HutWebsiteNormalized;
                hut.Link = string.Format(Helpers.HutBookingUrlV2, hutId);
                hut.Source = "AV"; // AV = Alpenverein, the source of the hut data
                hut.LastUpdated = DateTime.UtcNow;
                hut.Added = existingHut?.Added ?? DateTime.UtcNow;
                hut.Activated = existingHut?.Activated ?? (hutInfo.hutUnlocked ? DateTime.UtcNow : null);
                
                if (existingHut?.ManuallyEdited == true)
                {
                    _logger.LogInformation("Hut with ID={hutId} was manually edited. Not updating location information", hutId);
                }
                else
                {
                    hut.Country = hutInfo.CountryNormalized;
                    hut.Altitude = hutInfo.AltitudeInt;
                    hut.Longitude = hutInfo.Longitude;
                    hut.Latitude = hutInfo.Latitude;
                    
                    if (hut.Latitude == null || hut.Longitude == null || !Helpers.CoordinatesSanityCheck(hut.Longitude.Value, hut.Latitude.Value))
                    {
                        _logger.LogInformation("Hut with ID={hutId} has no or unrealistic coordinates. Trying to look up hut online", hutId);
                        var coordinates = await Helpers.SearchHutCoordinates(hutInfo.hutName, httpClient, _logger);

                        hut.Latitude = coordinates.latitude ?? existingHut?.Latitude;
                        hut.Longitude = coordinates.longitude ?? existingHut?.Longitude;
                    }

                    if (hut is { Latitude: not null, Longitude: not null })
                    {
                        var (country, region) = await Helpers.GetCountryAndRegion(hut.Latitude.Value, hut.Longitude.Value, _logger);
                        if (country != null)
                            hut.Country = country;
                        if (region != null)
                            hut.Region = region;
                    }
                }

                if (existingHut == null)
                    dbContext.Huts.Add(hut);

                await dbContext.SaveChangesAsync();

                return hut;
            }
            catch (Exception e)
            {
                _logger.LogError(default, e, "Exception in http call to provider: {Message}", e.Message);
            }

            return null;
        }
    }
}