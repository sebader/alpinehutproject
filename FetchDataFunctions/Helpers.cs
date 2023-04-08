using FetchDataFunctions.Models;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FetchDataFunctions
{
    public static class Helpers
    {
        public const string HutProviderBaseUrl = "https://www.alpsonline.org/reservation/calendar?";
        public const string SelectDateBaseUrl = "https://www.alpsonline.org/reservation/selectDate?date=";

        /// <summary>
        /// List of hut names which are clearly only used for testing. We filter these out
        /// </summary>
        public readonly static string[] ExcludedHutNames = new[]
        {
            "testhuette_elca, ELCA",
            "ZZZ TEST Monbijouhütte, SAC GS",
            "TEST123, TEST",
            "ZZZ TEST, TEST",
            "AV Testhütte, DAV Bundesgeschäftsstelle",
            "Test",
            "Domžalski dom Test",
            "ZZZ TEST - Demo Cabane CAS Gruyere, CAS Gruyere",
            "Testhütte Carolin"
        };

        public static AlpinehutsDbContext GetDbContext()
        {
            DbContextOptionsBuilder<AlpinehutsDbContext> optionsBuilder = new();

            var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");

            var dbConnection = new SqlConnection(connectionString);

            optionsBuilder.UseSqlServer(dbConnection, options => options.EnableRetryOnFailure());
            var alpinehutsDbContext = new AlpinehutsDbContext(optionsBuilder.Options);
            return alpinehutsDbContext;
        }

        /// <summary>
        /// Parse the html content from the hut booking page to scrape information like name, location, and hut website
        /// </summary>
        /// <param name="hutId"></param>
        /// <param name="doc"></param>
        /// <param name="isNewHut"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Hut> ParseHutInformation(int hutId, HtmlDocument doc, bool isNewHut, HttpClient httpClient, ILogger log)
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
                //hut.Coordinates = coordinates;

                var logoDiv = doc.DocumentNode.SelectSingleNode("//body").Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "logo").FirstOrDefault();
                var hutWebsiteUrl = logoDiv?.Descendants("a").Where(d => d.Attributes.Contains("href")).Select(a => a.Attributes["href"].Value).FirstOrDefault();
                if (!string.IsNullOrEmpty(hutWebsiteUrl))
                {
                    if (!hutWebsiteUrl.ToLower().StartsWith("http"))
                    {
                        hutWebsiteUrl = "http://" + hutWebsiteUrl;
                    }
                    hut.HutWebsite = hutWebsiteUrl;
                }
                hut.Enabled = !doc.ParsedText.Contains("Diese Hütte ist nicht freigeschaltet");

                string country = null;
                string region = null;

                // Only call the external services, if the hut is a new one for us
                if (isNewHut)
                {
                    var (latitude, longitude) = await SearchHutCoordinates(hutName, httpClient, log);

                    if (latitude != null && longitude != null)
                    {
                        hut.Latitude = latitude;
                        hut.Longitude = longitude;

                        var countryRegion = await GetCountryAndRegion((double)latitude, (double)longitude, httpClient, log);
                        country = countryRegion.country ?? country;
                        region = countryRegion.region;
                    }

                    if (string.IsNullOrEmpty(country))
                    {
                        // Fallback solution to roughly determine the country a hut is based in. This is not always accurate
                        country = GetCountry(hutName, phoneNumber, doc.ParsedText);
                    }
                }
                hut.Country = country;
                hut.Region = region;
                hut.LastUpdated = DateTime.UtcNow;

                log.LogInformation($"Hut info parsed: id={hutId} name={hut.Name} country={hut.Country} region={hut.Region} hutEnabled={hut.Enabled} lat={hut.Latitude} long={hut.Longitude}");
                return hut;
            }

            log.LogError("Please pass valid hut html page in the request body");
            return null;
        }

        private static string GetCountry(string hutName, string phoneNumber, string requestBody)
        {
            string[] swissNames = { "SAC", "CAS", "AACZ" };
            if (swissNames.Any(n => hutName.Contains(n)) || phoneNumber.Contains("+41") || phoneNumber.Contains("0041"))
            {
                return "Schweiz";
            }

            string[] southTyrolNames = { "AVS" };
            if (southTyrolNames.Any(n => hutName.Contains(n)) || phoneNumber.Contains("+39") || phoneNumber.Contains("0039"))
            {
                return "Italia";
            }

            if (phoneNumber.Contains("+43") || phoneNumber.Contains("0043"))
            {
                return "Österreich";
            }

            if (phoneNumber.Contains("+49") || phoneNumber.Contains("0049"))
            {
                return "Deutschland";
            }

            if (requestBody.Contains("de_CH"))
            {
                return "Schweiz";
            }
            else if (requestBody.Contains("de_AT"))
            {
                return "Österreich";
            }
            else if (requestBody.Contains("de_DE"))
            {
                return "unbekannt";
            }

            return "unbekannt";
        }

        /// <summary>
        /// Looks up the GPS coordinates based on a hut name
        /// Uses OSM Nominatim API https://nominatim.org/release-docs/latest/api/Search/
        /// </summary>
        /// <param name="hutName"></param>
        /// <returns></returns>
        public static async Task<(double? latitude, double? longitude)> SearchHutCoordinates(string hutName, HttpClient httpClient, ILogger log)
        {
            const string baseSearchUrl = "https://nominatim.openstreetmap.org/search.php?format=json&limit=5";
            string searchUrl = null;
            try
            {
                // Most hut names contain the Section after a comma. We only use the name for the search
                // Sample: "Linzer Tauplitz-Haus, Alpenverein Linz"
                if (hutName.Contains(','))
                {
                    hutName = hutName.Split(',')[0];
                }

                searchUrl = $"{baseSearchUrl}&q={hutName}";

                var result = await httpClient.GetAsync(searchUrl);
                result.EnsureSuccessStatusCode();

                var searchResults = await result.Content.ReadAsAsync<List<NominatimSearchResult>>();
                if (searchResults?.Count > 0)
                {
                    NominatimSearchResult sr = null;
                    if (searchResults.Count > 1)
                    {
                        // Many - but not all - of the huts actually have the type property properly 
                        sr = searchResults.FirstOrDefault(s => s.type == "alpine_hut" || s.type == "restaurant");
                        if (sr == null)
                        {
                            log.LogWarning($"Multiple coordinate search results for hut name={hutName}. Selecting the first one which might not be the correct one");
                        }
                    }
                    sr ??= searchResults.First();
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
                    return await SearchHutCoordinates(hutName, httpClient, log);
                }

                if (hutName.Contains(" Hütte", StringComparison.InvariantCultureIgnoreCase))
                {
                    hutName = hutName.Replace(" hütte", "hütte", StringComparison.InvariantCultureIgnoreCase);
                    log.LogInformation($"Attempting alternative search for {hutName}");
                    return await SearchHutCoordinates(hutName, httpClient, log);
                }

                if (hutName.Contains("hütte", StringComparison.InvariantCultureIgnoreCase) && !hutName.Contains("-hütte", StringComparison.InvariantCultureIgnoreCase))
                {
                    hutName = hutName.Replace("hütte", "-hütte", StringComparison.InvariantCultureIgnoreCase);
                    log.LogInformation($"Attempting alternative search for {hutName}");
                    return await SearchHutCoordinates(hutName, httpClient, log);
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, $"Exception while calling coordinate search for hut={hutName} with URL {searchUrl}");
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
        public static async Task<(string country, string region)> GetCountryAndRegion(double latitude, double longitude, HttpClient httpClient, ILogger log)
        {
            const string baseSearchUrl = "https://atlas.microsoft.com/search/address/reverse/json?api-version=1.0&language=de";

            string apiKey = Environment.GetEnvironmentVariable("AzureMapsApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("AzureMapsApiKey missing in AppSettings");
            }

            try
            {
                string searchUrl = $"{baseSearchUrl}&subscription-key={apiKey}&query={latitude.ToString("##.#####", CultureInfo.InvariantCulture)},{longitude.ToString("##.#####", CultureInfo.InvariantCulture)}";
                var result = await httpClient.GetAsync(searchUrl);
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
