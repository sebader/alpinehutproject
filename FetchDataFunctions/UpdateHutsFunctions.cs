using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FetchDataFunctions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

                return 700;
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

            var result = new List<Hut>();

            foreach (var hutId in hutIds.Split(','))
            {
                if (!int.TryParse(hutId, out int parsedId))
                {
                    _logger.LogWarning("Could not parse '{hutId}'. Ignoring", hutId);
                }

                var res = await GetHutFromProviderActivity(parsedId);
                if (res != null) result.Add(res);
            }

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
                    _logger.LogDebug("Found existing hut for id={hutId} in the database. name={HutName}", hutId,
                        existingHut.Name);
                }

                var httpClient = _clientFactory.CreateClient("HttpClient");

                // First we try to use locale de_DE, we might switch below
                var url = $"{Helpers.HutProviderBaseUrl}lang=de_DE&hut_id={hutId}";
                HtmlDocument doc = await LoadWebsite(url, httpClient);

                if (doc.ParsedText.Contains("kann nicht gefunden werden"))
                {
                    // Means there is no hut (yet) in the booking system with this id
                    _logger.LogInformation("Hut with ID={hutId} not found", hutId);
                }
                else
                {
                    // Check if the hut page is actually for a different german locale than de_DE. If so, we reload it with the correct locale
                    var languageSelector = doc.DocumentNode.SelectSingleNode("//body").Descendants("ul")
                        .Where(d => d.Id == "langSelector").FirstOrDefault();
                    var germanLocale = languageSelector?.Descendants("li").Where(d => d.InnerText == "Deutsch")
                        .Select(d => d.Id).FirstOrDefault();
                    if (!string.IsNullOrEmpty(germanLocale))
                    {
                        var newUrl = $"{Helpers.HutProviderBaseUrl}lang={germanLocale}&hut_id={hutId}";
                        if (newUrl != url)
                        {
                            url = newUrl;
                            doc = await LoadWebsite(url, httpClient);
                        }
                    }

                    var parsedHut =
                        await Helpers.ParseHutInformation(hutId, doc, (existingHut == null), httpClient, _logger);

                    if (Helpers.ExcludedHutNames.Contains(parsedHut?.Name))
                    {
                        _logger.LogInformation("Skipping excluded hut {hutName}", parsedHut?.Name);
                        return null;
                    }

                    if (parsedHut != null)
                    {
                        parsedHut.Link = url;

                        if (existingHut != null)
                        {
                            if (existingHut.Latitude == null || existingHut.Longitude == null ||
                                string.IsNullOrEmpty(existingHut.Country))
                            {
                                var (latitude, longitude) =
                                    await Helpers.SearchHutCoordinates(parsedHut.Name, httpClient, _logger);
                                if (latitude != null && longitude != null)
                                {
                                    parsedHut.Latitude = latitude;
                                    parsedHut.Longitude = longitude;

                                    var (country, region) = await Helpers.GetCountryAndRegion((double)latitude,
                                        (double)longitude, httpClient, _logger);
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
                        _logger.LogError("Error parsing hut page for ID={hutId}", hutId);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(default, e, "Exception in http call to provider");
            }

            return null;
        }

        private async Task<HtmlDocument> LoadWebsite(string url, HttpClient httpClient)
        {
            _logger.LogDebug("Executing http GET against {url}", url);

            var responseStream = await httpClient.GetStreamAsync(url);
            var doc = new HtmlDocument();
            doc.Load(responseStream);

            // Load the hut web page for parsing using HtmlAgilityPack
            //_logger.LogTrace($"HTTP Response body:\n {doc.ParsedText}");
            return doc;
        }
    }
}