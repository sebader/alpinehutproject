using FetchDataFunctions.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FetchDataFunctions
{
    public static class Helpers
    {
        public const string GetHutInfosUrlV2 = "https://www.hut-reservation.org/api/v1/reservation/hutInfo/{0}";
        public const string GetHutAvailabilityUrlV2 = "https://www.hut-reservation.org/api/v1/reservation/getHutAvailability?hutId={0}";
        public const string HutBookingUrlV2 = "https://www.hut-reservation.org/reservation/book-hut/{0}/wizard";

        /// <summary>
        /// List of hut names which are clearly only used for testing. We filter these out
        /// </summary>
        public static readonly string[] ExcludedHutNames =
        [
            "testhuette_elca",
            "ZZZ TEST Monbijouhütte",
            "TEST123",
            "ZZZ TEST",
            "AV Testhütte",
            "AVS Testhütte",
            "Test",
            "Domžalski dom Test",
            "ZZZ TEST - Demo Cabane CAS Gruyere, CAS Gruyere",
            "Testhütte Carolin",
            "Hüttenrunde Gipfelwege"
        ];

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
        /// Looks up the GPS coordinates based on a hut name
        /// Uses OSM Nominatim API https://nominatim.org/release-docs/latest/api/Search/
        /// </summary>
        /// <param name="hutName"></param>
        /// <param name="httpClient"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<(double? latitude, double? longitude)> SearchHutCoordinates(string hutName, HttpClient httpClient, ILogger log)
        {
            const string baseSearchUrl = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=5";
            string? searchUrl = null;
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

                var searchResults = await result.Content.ReadFromJsonAsync<List<NominatimSearchResult>>();
                if (searchResults?.Count > 0)
                {
                    NominatimSearchResult? sr = null;
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
                    if (!CoordinatesSanityCheck(lon, lat))
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
        /// <param name="httpClient"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<(string? country, string? region)> GetCountryAndRegion(double latitude, double longitude, HttpClient httpClient, ILogger log)
        {
            const string baseSearchUrl = "https://atlas.microsoft.com/search/address/reverse/json?api-version=1.0&language=de";

            var apiKey = Environment.GetEnvironmentVariable("AzureMapsApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("AzureMapsApiKey missing in AppSettings");
            }

            try
            {
                log.LogInformation($"Attempting to get country and region on Azure maps for coordinates={latitude},={longitude}");
                string searchUrl = $"{baseSearchUrl}&subscription-key={apiKey}&query={latitude.ToString("##.#####", CultureInfo.InvariantCulture)},{longitude.ToString("##.#####", CultureInfo.InvariantCulture)}";
                var result = await httpClient.GetAsync(searchUrl);
                result.EnsureSuccessStatusCode();

                var searchResult = await result.Content.ReadFromJsonAsync<AzureMapsResult>();
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
        
        public static bool CoordinatesSanityCheck(double longitude, double latitude)
        {
            // Do some simple sanity check if this location is somewhere in central Europe
            if (longitude < 4 || longitude > 17 || latitude > 53 || latitude < 44)
            {
                return false;
            }

            return true;
        }
    }
}