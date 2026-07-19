using System.Globalization;
using System.Net.Http.Json;
using FetchDataFunctions.Models;
using FetchDataFunctions.Models.HuettenHoliday;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions.Functions.HuettenHoliday;

public class HuettenHolidayGetHutsFromProvider(
    ILogger<HuettenHolidayGetHutsFromProvider> logger,
    IHttpClientFactory clientFactory,
    IDbContextFactory<AlpinehutsDbContext> dbContextFactory,
    TimeProvider timeProvider)
{
    private const int HutIdOffset = 10000; // Offset to avoid conflicts with other hut IDs

    [Function(nameof(HuettenHolidayUpdateHutHttpTriggered))]
    public async Task<IActionResult> HuettenHolidayUpdateHutHttpTriggered(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, string hutId, CancellationToken cancellationToken)
    {
        logger.LogInformation("HuettenHolidayUpdateHutHttpTriggered called with hutIds: {HutId}", hutId);

        var hutIdsList = hutId.Split(',').Select(i => int.Parse(i, CultureInfo.InvariantCulture) + HutIdOffset).ToList();

        // Get all huts from the provider, then filter by hutId
        var allHuts = await HuettenHolidayUpdateHutsActivityTrigger(null, cancellationToken);
        var huts = allHuts?.Where(h => hutIdsList.Contains(h.Id)).ToList();
        if (huts == null)
        {
            return new NotFoundObjectResult("No huts found.");
        }

        return new OkObjectResult(huts);
    }

    [Function(nameof(HuettenHolidayUpdateHutsActivityTrigger))]
    public async Task<IEnumerable<Hut>?> HuettenHolidayUpdateHutsActivityTrigger([ActivityTrigger] string? input, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var httpClient = clientFactory.CreateClient("HttpClient");
            var url = "https://www.huetten-holiday.com/get-cabins?page=1";
            var huts = new List<Hut>();
            do
            {
                logger.LogInformation("Fetching huts from HuettenHoliday: {Url}", url);
                var response = await httpClient.GetFromJsonAsync<GetCabinsResult>(url, ScraperJson.Options, cancellationToken);

                if (response == null)
                {
                    logger.LogWarning("No huts found on page");
                    break;
                }

                foreach (var cabin in response.data)
                {
                    if (cabin.is_delete)
                    {
                        logger.LogInformation("Skipping deleted cabin with ID {CabinId}", cabin.id);
                        continue;
                    }

                    cabin.id += HutIdOffset;

                    if (cabin.website != null)
                    {
                        if (!cabin.website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !cabin.website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            cabin.website = "https://" + cabin.website;
                        }
                    }

                    string? country = null;
                    string? region = null;
                    if (cabin.latitude != null && cabin.longitude != null)
                    {
                        var countryAndRegion = await Helpers.GetCountryAndRegion(cabin.latitude.Value, cabin.longitude.Value, logger, cancellationToken);
                        country = countryAndRegion.country;
                        region = countryAndRegion.region;
                    }

                    var hut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == cabin.id, cancellationToken);

                    var now = timeProvider.GetUtcNow().UtcDateTime;
                    if (hut == null)
                    {
                        logger.LogInformation("Creating new hut object for cabin with ID {CabinId}", cabin.id);

                        hut = new Hut
                        {
                            Id = cabin.id,
                            Source = "HuettenHoliday",
                            Name = cabin.name,
                            HutWebsite = cabin.website,
                            Link = $"https://www.huetten-holiday.com/huts/{cabin.slug}",
                            Latitude = cabin.latitude,
                            Longitude = cabin.longitude,
                            Altitude = (int?)cabin.altitude,
                            Country = !string.IsNullOrEmpty(cabin.country.name.de) ? cabin.country.name.de : country,
                            Region = region,
                            Enabled = true,
                            Added = now,
                            LastUpdated = now,
                        };
                        dbContext.Add(hut);
                    }
                    else
                    {
                        if (hut.ManuallyEdited == true)
                        {
                            logger.LogInformation("Hut with ID {HutId} was manually edited. Not updating hut information", hut.Id);
                            continue; // Skip updating manually edited huts
                        }

                        // Update existing hut
                        logger.LogInformation("Updating existing hut with ID {HutId}", hut.Id);
                        hut.Name = cabin.name;
                        hut.HutWebsite = cabin.website;
                        hut.Link = $"https://www.huetten-holiday.com/huts/{cabin.slug}";
                        hut.Latitude = cabin.latitude;
                        hut.Longitude = cabin.longitude;
                        hut.Altitude = (int?)cabin.altitude;
                        hut.Country = !string.IsNullOrEmpty(cabin.country.name.de) ? cabin.country.name.de : country;
                        hut.Region = region;
                        hut.Enabled = true;
                        hut.LastUpdated = now;
                    }

                    huts.Add(hut);
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                url = response.next_page_url ?? null;
            } while (url != null);

            logger.LogInformation("Fetched {HutCount} huts from HuettenHoliday", huts.Count);
            return huts;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while fetching huts from HuettenHoliday");
            return null;
        }
    }
}
