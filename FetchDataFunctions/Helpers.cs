using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Core.GeoJson;
using Azure.Identity;
using Azure.Maps.Search;
using FetchDataFunctions.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<(string? country, string? region)> GetCountryAndRegion(double latitude, double longitude, ILogger log)
        {
            // Azure Maps (and any WGS84 reverse geocoder) only accepts latitude in [-90, 90] and longitude in [-180, 180].
            // Some providers return coordinates in a projected / national grid system instead of WGS84 - e.g. certain
            // Swiss huts from hut-reservation.org come as CH1903 / LV03 kilometres (values like 587.168 / 234.694).
            // Those would produce a guaranteed BadRequest, so skip the lookup rather than calling the API.
            if (!IsValidWgs84Coordinate(latitude, longitude))
            {
                log.LogWarning($"Skipping reverse address lookup on Azure maps for out-of-range coordinates={latitude},{longitude}. These are not valid WGS84 coordinates (looks like a projected / national grid system)");
                return (null, null);
            }

            var apiKey = Environment.GetEnvironmentVariable("AzureMaps__ApiKey");

            var searchOptions = new MapsSearchClientOptions(language: SearchLanguage.German);
            MapsSearchClient client;
            if (!string.IsNullOrEmpty(apiKey))
            {
                client = new MapsSearchClient(new AzureKeyCredential(apiKey), searchOptions);
            }
            else
            {
                var azureMapsClientId = Environment.GetEnvironmentVariable("AzureMaps__clientId");
                var credentialClientId = Environment.GetEnvironmentVariable("AzureMaps__Credential__clientId");
                client = new MapsSearchClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = credentialClientId }), azureMapsClientId, searchOptions);
            }

            try
            {
                log.LogInformation($"Attempting to get country and region on Azure maps for coordinates={latitude},{longitude}");

                var mapsSearchResult = await client.GetReverseGeocodingAsync(new GeoPosition(longitude, latitude));
                var address = mapsSearchResult?.Value?.Features?.FirstOrDefault()?.Properties?.Address;
                if (address == null)
                {
                    log.LogWarning($"No result for reverse address lookup on Azure maps for coordinates={latitude},{longitude}");
                }
                else
                {
                    var district = address.AdminDistricts.FirstOrDefault()?.Name;
                    var country = address.CountryRegion.Name;
                    return (country, district);
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

        /// <summary>
        /// Checks whether the given coordinates are valid WGS84 values, i.e. latitude within [-90, 90]
        /// and longitude within [-180, 180]. Anything outside (including NaN / infinity) is rejected.
        /// </summary>
        public static bool IsValidWgs84Coordinate(double latitude, double longitude)
        {
            return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        }

        /// <summary>
        /// Some providers (e.g. certain Swiss huts on hut-reservation.org) return coordinates in the Swiss national
        /// grid (CH1903 / LV03 or CH1903+ / LV95) instead of WGS84. This tries to detect such a pair of values and
        /// convert it to WGS84 latitude/longitude. Returns false if the values are not recognizable Swiss grid coordinates.
        /// The two input values may be given in either order and either kilometres or metres.
        /// </summary>
        public static bool TryConvertSwissGridToWgs84(double value1, double value2, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;

            // In the Swiss grid the easting (E/Y) is always larger than the northing (N/X), so we can identify them
            // regardless of the order they were stored in.
            var easting = Math.Max(value1, value2);
            var northing = Math.Min(value1, value2);

            // Values may arrive in kilometres (e.g. 587.168) or metres (587168). Scale up to metres if needed.
            while (easting is > 0 and < 480000)
            {
                easting *= 1000;
                northing *= 1000;
            }

            // Convert LV95 (CH1903+, false origin 2 600 000 / 1 200 000) to LV03 by removing the offsets.
            if (easting >= 2000000)
            {
                easting -= 2000000;
                northing -= 1000000;
            }

            // Only accept values that fall inside the Swiss LV03 bounding box, otherwise this is not Swiss grid data.
            if (easting is < 480000 or > 840000 || northing is < 70000 or > 300000)
            {
                return false;
            }

            (latitude, longitude) = Ch1903ToWgs84(easting, northing);
            return true;
        }

        /// <summary>
        /// Converts CH1903 / LV03 coordinates (in metres) to WGS84 latitude/longitude using swisstopo's
        /// approximate formulas (accuracy ~1 m), which is more than enough for map display.
        /// See https://www.swisstopo.admin.ch/en/coordinates-conversion-navref
        /// </summary>
        public static (double latitude, double longitude) Ch1903ToWgs84(double easting, double northing)
        {
            // Auxiliary values relative to the projection origin in Bern.
            var y = (easting - 600000) / 1_000_000;
            var x = (northing - 200000) / 1_000_000;

            var longitude = 2.6779094
                + 4.728982 * y
                + 0.791484 * y * x
                + 0.1306 * y * x * x
                - 0.0436 * y * y * y;

            var latitude = 16.9023892
                + 3.238272 * x
                - 0.270978 * y * y
                - 0.002528 * x * x
                - 0.0447 * y * y * x
                - 0.0140 * x * x * x;

            // The formulas yield values in units of 10000"; convert to decimal degrees.
            return (latitude * 100 / 36, longitude * 100 / 36);
        }
    }
}