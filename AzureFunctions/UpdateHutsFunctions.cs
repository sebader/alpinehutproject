using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace AzureFunctions
{
    public static class UpdateHutsFunctions
    {
        private const int MaxHutId = 300;
        private const int ParallelTasks = 100;

        private static HttpClient _httpClient = new HttpClient();

        [FunctionName("UpdateHutsTimerTriggered")]
        public static async Task UpdateHutsTimerTriggered([TimerTrigger("0 0 0 * * 0", RunOnStartup = false)]TimerInfo myTimer,
            ILogger log,
            [OrchestrationClient] DurableOrchestrationClient starter)
        {
            if(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }
            string instanceId = await starter.StartNewAsync("UpdateHutsOrchestrator", 1);
            log.LogInformation($"UpdateHut orchestrator started. Instance ID={instanceId}");
        }

        /// <summary>
        /// This function can update a single hut. More for debugging.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("UpdateHutHttpTriggered")]
        public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string hutIds = req.Query["hutid"];
            if (string.IsNullOrEmpty(hutIds))
            {
                return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
            }

            var result = new List<Hut>();

            foreach (var hutId in hutIds.Split(','))
            {
                int parsedId;
                if (!int.TryParse(hutId, out parsedId))
                {
                    log.LogWarning($"Could not parse '{hutId}'. Ignoring");
                }

                var res = await GetHutFromProviderActivity(parsedId, log);
                result.Add(res);
            }
            return new OkObjectResult(result);
        }

        [FunctionName("UpdateHutsOrchestrator")]
        public static async Task UpdateHutsOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            int startHutId = context.GetInput<int>();
            if (!context.IsReplaying)
            {
                log.LogInformation($"Starting orchestrator with startHutId={startHutId}");
            }
            var tasks = new Task[ParallelTasks];

            // Fan-out
            for (int i = 0; i < ParallelTasks; i++)
            {
                tasks[i] = context.CallActivityAsync("GetHutFromProvider", i + startHutId);
            }

            await Task.WhenAll(tasks);

            if (!context.IsReplaying)
            {
                log.LogInformation($"Update hut orchestrator finished batch from {startHutId} to {startHutId + ParallelTasks}");
            }

            int nextStartId = startHutId + ParallelTasks;

            if (nextStartId <= MaxHutId)
            {
                context.ContinueAsNew(nextStartId);
            }
            else
            {
                log.LogInformation($"MaxHutId {MaxHutId} reached. Ending hut updating now.");
            }
        }


        [FunctionName("GetHutFromProvider")]
        public static async Task<Hut> GetHutFromProviderActivity([ActivityTrigger] int hutId, ILogger log)
        {
            try
            {
                log.LogInformation($"Executing GetHutFromProviderActivity for hutid={hutId}");
                var dbContext = Helpers.GetDbContext();

                var existingHut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == hutId);
                if (existingHut == null)
                {
                    log.LogInformation($"No hut yet in the database for id={hutId}");
                }

                var response = await _httpClient.GetAsync($"{Helpers.HutProviderBaseUrl}{hutId}");
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseBody) && !responseBody.Contains("kann nicht gefunden werden"))
                {
                    var parsedHut = await Helpers.ParseHutInformation(hutId, responseBody, (existingHut == null), log);
                    if (parsedHut != null)
                    {
                        parsedHut.Link = $"{Helpers.HutProviderBaseUrl}{hutId}";
                        parsedHut.LastUpdated = DateTime.UtcNow;

                        if (existingHut != null)
                        {
                            // We only call out to the external services (MapQuest and Azure Maps) if the name changed or if country/region are null yet (searches are based on the name)
                            if (existingHut.Name != parsedHut.Name || existingHut.Country == null || existingHut.Region == null)
                            {
                                var latLong = await Helpers.SearchHutCoordinates(parsedHut.Name, log);
                                if (latLong.latitude != null && latLong.longitude != null)
                                {
                                    parsedHut.Latitude = latLong.latitude;
                                    parsedHut.Longitude = latLong.longitude;

                                    var countryRegion = await Helpers.GetCountryAndRegion((double)latLong.latitude, (double)latLong.longitude, log);
                                    parsedHut.Country = countryRegion.country ?? parsedHut.Country;
                                    parsedHut.Region = countryRegion.region;
                                }
                            }
                            existingHut.Name = parsedHut.Name;
                            existingHut.Enabled = parsedHut.Enabled;
                            existingHut.Latitude = parsedHut.Latitude ?? existingHut.Latitude;
                            existingHut.Longitude = parsedHut.Longitude ?? existingHut.Longitude;
                            existingHut.Country = parsedHut.Country ?? existingHut.Country;
                            existingHut.Region = parsedHut.Region ?? existingHut.Region;
                            existingHut.LastUpdated = DateTime.UtcNow;
                            dbContext.Update(existingHut);
                        }
                        else
                        {
                            dbContext.Huts.Add(parsedHut);
                        }

                        await dbContext.SaveChangesAsync();

                        return existingHut ?? parsedHut;
                    }
                    else
                    {
                        log.LogError($"Error parsing hut page for ID={hutId}");
                    }
                }
                else
                {
                    log.LogInformation($"Hut with ID={hutId} not found");
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in http call to provider");
            }

            return null;
        }

        [FunctionName("UpsertHuts")]
        public static async Task<int> UpsertHuts([ActivityTrigger] IList<Hut> huts, ILogger log)
        {
            try
            {
                var dbContext = Helpers.GetDbContext();

                foreach (var hut in huts)
                {
                    var existingHut = await dbContext.Huts.FirstOrDefaultAsync(h => h.Id == hut.Id);
                    if (existingHut != null)
                    {
                        log.LogInformation($"Updating existing hut ID={hut.Id} name={hut.Name}");
                        existingHut.Name = !string.IsNullOrEmpty(hut.Name) ? hut.Name : existingHut.Name;
                        existingHut.Link = !string.IsNullOrEmpty(hut.Link) ? hut.Link : existingHut.Link;
                        existingHut.Coordinates = !string.IsNullOrEmpty(hut.Coordinates) ? hut.Coordinates : existingHut.Coordinates;
                        existingHut.Country = !string.IsNullOrEmpty(hut.Country) ? hut.Country : existingHut.Country;
                        existingHut.Region = !string.IsNullOrEmpty(hut.Region) ? hut.Region : existingHut.Region;
                        existingHut.Enabled = hut.Enabled ?? existingHut.Enabled;
                        existingHut.Latitude = hut.Latitude ?? existingHut.Latitude;
                        existingHut.Longitude = hut.Longitude ?? existingHut.Longitude;
                        existingHut.LastUpdated = hut.LastUpdated;
                        dbContext.Update(existingHut);
                    }
                    else
                    {
                        log.LogInformation($"Adding new hut ID={hut.Id} name={hut.Name}");
                        dbContext.Huts.Add(hut);
                    }
                }

                return await dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in writing hut updates to database");
                return -1;
            }
        }
    }
}
