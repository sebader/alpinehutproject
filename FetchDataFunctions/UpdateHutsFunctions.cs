using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FetchDataFunctions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FetchDataFunctions
{
    public class UpdateHutsFunctions
    {
        private static int MaxHutId
        {
            get
            {
                if (int.TryParse(Environment.GetEnvironmentVariable("MAX_HUT_ID"), out int value))
                {
                    return value;
                }
                return 600;
            }
        }

        private readonly IHttpClientFactory _clientFactory;
        public UpdateHutsFunctions(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [FunctionName(nameof(UpdateHutsTimerTriggered))]
        public async Task UpdateHutsTimerTriggered([TimerTrigger("0 0 2 * * *", RunOnStartup = false)] TimerInfo myTimer,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }
            // Start hutId should start at least with 1, not 0, thats why we add 1
            int startHutId = (int)DateTime.UtcNow.DayOfWeek + 1;
            string instanceId = await starter.StartNewAsync<int>(nameof(UpdateHutsOrchestrator), null, startHutId);
            log.LogInformation($"UpdateHut orchestrator started. Instance ID={instanceId}");
        }

        /// <summary>
        /// This function can update a single hut. More for debugging than actual live use.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName(nameof(UpdateHutHttpTriggered))]
        public async Task<IActionResult> UpdateHutHttpTriggered(
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
                if (!int.TryParse(hutId, out int parsedId))
                {
                    log.LogWarning("Could not parse '{hutId}'. Ignoring", hutId);
                }

                var res = await GetHutFromProviderActivity(parsedId, log);
                result.Add(res);
            }
            return new OkObjectResult(result);
        }

        [FunctionName(nameof(UpdateHutsOrchestrator))]
        public async Task UpdateHutsOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            log = context.CreateReplaySafeLogger(log);
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


        [FunctionName(nameof(GetHutFromProviderActivity))]
        public async Task<Hut> GetHutFromProviderActivity([ActivityTrigger] int hutId, ILogger log)
        {
            try
            {
                log.LogInformation("Executing " + nameof(GetHutFromProviderActivity) + " for hutid={hutId}", hutId);
                var dbContext = Helpers.GetDbContext();

                var existingHut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == hutId);
                if (existingHut == null)
                {
                    log.LogInformation("No hut yet in the database for id={hutId}", hutId);
                }
                else
                {
                    log.LogDebug("Found existing hut for id={hutId} in the database. name={HutName}", hutId, existingHut.Name);
                }

                var httpClient = _clientFactory.CreateClient("HttpClient");

                // First we try to use locale de_DE, we might switch below
                var url = $"{Helpers.HutProviderBaseUrl}lang=de_DE&hut_id={hutId}";
                HtmlDocument doc = await LoadWebsite(url, httpClient, log);

                if (doc.ParsedText.Contains("kann nicht gefunden werden"))
                {
                    // Means there is no hut (yet) in the booking system with this id
                    log.LogInformation("Hut with ID={hutId} not found", hutId);
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
                            doc = await LoadWebsite(url, httpClient, log);
                        }
                    }
                    var parsedHut = await Helpers.ParseHutInformation(hutId, doc, (existingHut == null), httpClient, log);

                    if (Helpers.ExcludedHutNames.Contains(parsedHut.Name))
                    {
                        log.LogInformation("Skipping excluded hut {hutName}", parsedHut.Name);
                        return null;
                    }

                    if (parsedHut != null)
                    {
                        parsedHut.Link = url;

                        if (existingHut != null)
                        {
                            if (existingHut.Latitude == null || existingHut.Longitude == null || string.IsNullOrEmpty(existingHut.Country))
                            {
                                var (latitude, longitude) = await Helpers.SearchHutCoordinates(parsedHut.Name, httpClient, log);
                                if (latitude != null && longitude != null)
                                {
                                    parsedHut.Latitude = latitude;
                                    parsedHut.Longitude = longitude;

                                    var (country, region) = await Helpers.GetCountryAndRegion((double)latitude, (double)longitude, httpClient, log);
                                    parsedHut.Country = country ?? parsedHut.Country;
                                    parsedHut.Region = region ?? parsedHut.Region;
                                }
                            }
                            existingHut.Name = parsedHut.Name;
                            if (existingHut.Enabled == false && parsedHut.Enabled == true)
                            {
                                existingHut.Activated = DateTime.Today;
                            }
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
                            parsedHut.Added = DateTime.Today;
                            if (parsedHut.Enabled == true)
                            {
                                parsedHut.Activated = DateTime.Today;
                            }
                            dbContext.Huts.Add(parsedHut);
                        }

                        await dbContext.SaveChangesAsync();

                        return existingHut ?? parsedHut;
                    }
                    else
                    {
                        log.LogError("Error parsing hut page for ID={hutId}", hutId);
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in http call to provider");
            }

            return null;
        }

        private static async Task<HtmlDocument> LoadWebsite(string url, HttpClient httpClient, ILogger log)
        {
            log.LogDebug("Executing http GET against {url}", url);

            var responseStream = await httpClient.GetStreamAsync(url);
            var doc = new HtmlDocument();
            doc.Load(responseStream);

            // Load the hut web page for parsing using HtmlAgilityPack
            //log.LogTrace($"HTTP Response body:\n {doc.ParsedText}");
            return doc;
        }
    }
}
