﻿using AzureFunctions.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AzureFunctions
{
    public static class Helpers
    {
        public const string HutProviderBaseUrl = "https://www.alpsonline.org/reservation/calendar?lang=de_DE&hut_id=";
        public const string SelectDateBaseUrl = "https://www.alpsonline.org/reservation/selectDate?date=";

        public static IConfigurationRoot config = new ConfigurationBuilder()
        .SetBasePath(Environment.CurrentDirectory)
        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

        private static HttpClient _httpClient = new HttpClient();

        public static AlpinehutsDbContext GetDbContext()
        {
            DbContextOptionsBuilder<AlpinehutsDbContext> optionsBuilder = new DbContextOptionsBuilder<AlpinehutsDbContext>();
            optionsBuilder.UseSqlServer(config["DatabaseConnectionString"], options => options.EnableRetryOnFailure());
            var alpinehutsDbContext = new AlpinehutsDbContext(optionsBuilder.Options);
            return alpinehutsDbContext;
        }

        public static async Task<Hut> ParseHutInformation(int hutId, HtmlDocument doc, bool isNewHut, ILogger log)
        {
            var infoDiv = doc.DocumentNode.SelectSingleNode("//body").Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "info").FirstOrDefault();
            if (infoDiv != null)
            {
                string hutName = infoDiv.ChildNodes["h4"].InnerText;
                if (string.IsNullOrEmpty(hutName))
                {
                    log.LogWarning($"HutName empty. Ignoring hutid={hutId}");
                    return null;
                }
                var hut = new Hut() { Id = hutId, Name = hutName };
                var spans = infoDiv.ChildNodes.Where(c => c.Name == "span").ToArray();
                // We use the phone number only as a way to determine the country
                string phoneNumber = spans[1]?.InnerText;
                log.LogDebug($"Phonenumber={phoneNumber}");

                string coordinates = spans[4]?.InnerText;

                coordinates = Regex.Replace(coordinates, @"\s+", " ");
                coordinates = Regex.Replace(coordinates, "Koordinaten: ", "");
                hut.Coordinates = coordinates;

                hut.Enabled = !doc.ParsedText.Contains("Diese Hütte ist nicht freigeschaltet");

                string country = null;
                string region = null;

                // Only call the external services, if the hut is a new one for us
                if (isNewHut)
                {
                    country = GetCountry(hutName, phoneNumber, doc.ParsedText);

                    var latLong = await SearchHutCoordinates(hutName, log);

                    if (latLong.latitude != null && latLong.longitude != null)
                    {
                        hut.Latitude = latLong.latitude;
                        hut.Longitude = latLong.longitude;

                        var countryRegion = await GetCountryAndRegion((double)latLong.latitude, (double)latLong.longitude, log);
                        country = countryRegion.country ?? country;
                        region = countryRegion.region;
                    }
                }
                hut.Country = country;
                hut.Region = region;
                hut.LastUpdated = DateTime.UtcNow;

                log.LogInformation($"Hut info parsed: id={hutId} name={hut.Name} country={hut.Country} region={hut.Region} hutEnabled={hut.Enabled} lat={hut.Latitude} long={hut.Longitude} coordinates={hut.Coordinates}");
                return hut;
            }

            log.LogError("Please pass valid hut html page in the request body");
            return null;
        }

        private static string GetCountry(string hutName, string phoneNumber, string requestBody)
        {
            string[] swissNames = { "SAC", "CAS", "AACZ" };
            if (swissNames.Any(n => hutName.Contains(n) || phoneNumber.Contains("+41") || phoneNumber.Contains("0041")))
            {
                return "Switzerland";
            }

            string[] southTyrolNames = { "AVS" };
            if (southTyrolNames.Any(n => hutName.Contains(n) || phoneNumber.Contains("+39") || phoneNumber.Contains("0039")))
            {
                return "Italy";
            }

            if (phoneNumber.Contains("+43") || phoneNumber.Contains("0043"))
            {
                return "Austria";
            }

            if (phoneNumber.Contains("+49") || phoneNumber.Contains("0049"))
            {
                return "Germany";
            }

            if (requestBody.Contains("de_CH"))
            {
                return "Switzerland";
            }
            else if (requestBody.Contains("de_AT"))
            {
                return "Austria";
            }
            else if (requestBody.Contains("de_DE"))
            {
                return "Germany/Austria";
            }

            return "Germany/Austria";
        }

        /// <summary>
        /// Looks up the GPS coordinates based on a hut name
        /// Uses MapQuest API https://developer.mapquest.com/documentation/open/nominatim-search/search/
        /// This data is based on OpenStreetMap
        /// </summary>
        /// <param name="hutName"></param>
        /// <returns></returns>
        public static async Task<(double? latitude, double? longitude)> SearchHutCoordinates(string hutName, ILogger log)
        {
            const string baseSearchUrl = "https://open.mapquestapi.com/nominatim/v1/search.php?format=json&limit=5";

            string apiKey = config["MapQuestApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("MapQuestApiKey missing in AppSettings");
            }

            try
            {
                // Most hut names contain the Section after a comma. We only use the name for the search
                // Sample: "Linzer Tauplitz-Haus, Alpenverein Linz"
                if (hutName.Contains(','))
                {
                    hutName = hutName.Split(',')[0];
                }

                string searchUrl = $"{baseSearchUrl}&key={apiKey}&q={hutName}";
                var result = await _httpClient.GetAsync(searchUrl);
                result.EnsureSuccessStatusCode();

                var searchResults = await result.Content.ReadAsAsync<List<MapQuestSearchResult>>();
                if (searchResults?.Count > 0)
                {
                    MapQuestSearchResult sr = null;
                    if (searchResults.Count > 1)
                    {
                        // Many - but not all - of the huts actually have the type property properly 
                        sr = searchResults.FirstOrDefault(s => s.type == "alpine_hut" || s.type == "restaurant");
                        if (sr == null)
                        {
                            log.LogWarning($"Multiple coordinate search results for hut name={hutName}. Selecting the first one which might not be the correct one");
                        }
                    }
                    sr = sr ?? searchResults.First();
                    var lat = double.Parse(sr.lat, CultureInfo.InvariantCulture);
                    var lon = double.Parse(sr.lon, CultureInfo.InvariantCulture);

                    // Do some simple sanity check if this location is somewhere in central Europe
                    if (lon < 4 || lon > 17 || lat > 53 || lat < 44)
                    {
                        log.LogWarning($"Unrealistic coordinates found for hut={hutName} lat={lat} long={lon}. Discarding result");
                    }
                    else
                    {
                        log.LogInformation($"Got location for hut={hutName} lat={lat} long={lon}");
                        return (lat, lon);
                    }
                }
                log.LogWarning($"No coordinate result for hut name={hutName}");

                if (Regex.IsMatch(hutName, "(?<TLA> [A-ZÖÄÜ]{2,4})"))
                {
                    hutName = Regex.Replace(hutName, "(?<TLA> [A-ZÖÄÜ]{2,4})", "");
                    log.LogInformation($"Attempting alternative search for {hutName} without TLA");
                    return await SearchHutCoordinates(hutName, log);
                }

                if (hutName.Contains(" Hütte", StringComparison.InvariantCultureIgnoreCase))
                {
                    hutName = hutName.Replace(" hütte", "hütte", StringComparison.InvariantCultureIgnoreCase);
                    log.LogInformation($"Attempting alternative search for {hutName}");
                    return await SearchHutCoordinates(hutName, log);
                }

                if (hutName.Contains("hütte", StringComparison.InvariantCultureIgnoreCase) && !hutName.Contains("-hütte", StringComparison.InvariantCultureIgnoreCase))
                {
                    hutName = hutName.Replace("hütte", "-hütte", StringComparison.InvariantCultureIgnoreCase);
                    log.LogInformation($"Attempting alternative search for {hutName}");
                    return await SearchHutCoordinates(hutName, log);
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, $"Exception while calling coordinate search for hut={hutName}");
            }
            return (null, null);
        }

        /// <summary>
        /// Looks up the country and region for a given coordinate using Azure Maps API
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<(string country, string region)> GetCountryAndRegion(double latitude, double longitude, ILogger log)
        {
            const string baseSearchUrl = "https://atlas.microsoft.com/search/address/reverse/json?api-version=1.0";

            string apiKey = config["AzureMapsApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("AzureMapsApiKey missing in AppSettings");
            }

            try
            {
                string searchUrl = $"{baseSearchUrl}&subscription-key={apiKey}&query={latitude.ToString("##.#####", CultureInfo.InvariantCulture)},{longitude.ToString("##.#####", CultureInfo.InvariantCulture)}";
                var result = await _httpClient.GetAsync(searchUrl);
                result.EnsureSuccessStatusCode();

                var searchResult = await result.Content.ReadAsAsync<AzureMapsResult>();
                if (searchResult?.addresses?.Length > 0)
                {
                    var address = searchResult.addresses.First().address;
                    return (address.country, address.countrySubdivision);
                }
                else
                {
                    log.LogWarning($"No result for reverse address lookup on Azure maps for coordinates={latitude},{longitude}");
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, $"Exception while calling reverse address lookup on Azure maps for coordinates={latitude},{longitude}");
            }

            return (null, null);
        }
    }
}