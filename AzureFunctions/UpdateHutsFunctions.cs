using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
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
        private const int MaxHutId = 320;

        [FunctionName("UpdateHutsTimerTriggered")]
        public static async Task UpdateHutsTimerTriggered([TimerTrigger("0 0 0 * * 0", RunOnStartup = false)]TimerInfo myTimer,
            ILogger log,
            [OrchestrationClient] DurableOrchestrationClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
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
            log.LogInformation("Update Hut HTTP trigger function received a request");

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
            var tasks = new Task[MaxHutId + 1];

            // Fan-out
            for (int i = startHutId; i <= MaxHutId; i++)
            {
                tasks[i] = context.CallActivityAsync("GetHutFromProvider", i);
            }

            await Task.WhenAll(tasks);

            log.LogInformation($"MaxHutId {MaxHutId} reached. Ending hut updating now.");
        }


        [FunctionName("GetHutFromProvider")]
        public static async Task<Hut> GetHutFromProviderActivity([ActivityTrigger] int hutId, ILogger log)
        {
            try
            {
                log.LogInformation($"Executing GetHutFromProviderActivity for hutid={hutId}");
                var dbContext = await Helpers.GetDbContext();

                var existingHut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == hutId);
                if (existingHut == null)
                {
                    log.LogInformation($"No hut yet in the database for id={hutId}");
                }
                else
                {
                    log.LogDebug($"Found existing hut for id={hutId} in the database. name={existingHut.Name}");
                }

                // First we try to use locale de_DE, we might switch below
                var url = $"{Helpers.HutProviderBaseUrl}lang=de_DE&hut_id={hutId}";
                HtmlDocument doc = await LoadWebsite(url, log);

                if (doc.ParsedText.Contains("kann nicht gefunden werden"))
                {
                    // Means there is no hut (yet) in the booking system with this id
                    log.LogInformation($"Hut with ID={hutId} not found");
                }
                else
                {
                    // Check if the hut page is actually for a different german locale than de_DE. If so, we reload it with the correct locale
                    var languageSelector = doc.DocumentNode.SelectSingleNode("//body").Descendants("ul").Where(d => d.Id == "langSelector").FirstOrDefault();
                    var germanLocale = languageSelector?.Descendants("li").Where(d => d.InnerText == "Deutsch").Select(d => d.Id).FirstOrDefault();
                    if (!string.IsNullOrEmpty(germanLocale))
                    {
                        var newUrl = $"{Helpers.HutProviderBaseUrl}lang={germanLocale}&hut_id={hutId}";
                        if (newUrl != url)
                        {
                            url = newUrl;
                            doc = await LoadWebsite(url, log);
                        }
                    }

                    var parsedHut = await Helpers.ParseHutInformation(hutId, doc, (existingHut == null), log);
                    if (parsedHut != null)
                    {
                        parsedHut.Link = url;

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
                            existingHut.Link = parsedHut.Link;
                            existingHut.HutWebsite = parsedHut.HutWebsite;
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
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in http call to provider");
            }

            return null;
        }

        private static async Task<HtmlDocument> LoadWebsite(string url, ILogger log)
        {
            log.LogDebug($"Executing http GET against {url}");
            // Load the hut web page for parsing using HtmlAgilityPack
            var web = new HtmlWeb();
            web.UsingCache = false;
            web.CaptureRedirect = false;
            var doc = await web.LoadFromWebAsync(url);

            //log.LogTrace($"HTTP Response body:\n {doc.ParsedText}");
            return doc;
        }
    }
}
