using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FetchDataFunctions.Models;
using FetchDataFunctions.Models.HuettenHoliday;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions.Functions.HuettenHoliday;

public class HuettenHolidayGetHutFromProvider
{
    private readonly ILogger<HuettenHolidayGetHutFromProvider> _logger;
    private readonly IHttpClientFactory _clientFactory;

    private const int HutIdOffset = 10000; // Offset to avoid conflicts with other hut IDs

    public HuettenHolidayGetHutFromProvider(ILogger<HuettenHolidayGetHutFromProvider> logger, IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _clientFactory = clientFactory;
    }

    [Function(nameof(HuettenHolidayUpdateHutHttpTriggered))]
    public async Task<IActionResult> HuettenHolidayUpdateHutHttpTriggered([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, string hutId)
    {
        _logger.LogInformation("HuettenHolidayUpdateHutHttpTriggered called with hutIds: {HutId}", hutId);

        var hutIdsList = hutId.Split(',').Select(i => int.Parse(i) + HutIdOffset).ToList();
        
        // Get all huts from the provider, then filter by hutId
        var allHuts = await HuettenHolidayGetHutsFromProvider(null);
        var huts = allHuts?.Where(h => hutIdsList.Contains(h.Id)).ToList();
        if (huts == null)
        {
            return new NotFoundObjectResult("No huts found.");
        }

        return new OkObjectResult(huts);
    }

    [Function(nameof(HuettenHolidayGetHutFromProvider))]
    public async Task<IEnumerable<Hut>?> HuettenHolidayGetHutsFromProvider([ActivityTrigger] string? input)
    {
        try
        {
            var dbContext = Helpers.GetDbContext();

            var httpClient = _clientFactory.CreateClient("HttpClient");
            var url = "https://www.huetten-holiday.com/get-cabins?page=1";
            var huts = new List<Hut>();
            do
            {
                _logger.LogInformation("Fetching huts from HuettenHoliday: {Url}", url);
                var response = await httpClient.GetFromJsonAsync<GetCabinsResult>(url);

                if (response == null)
                {
                    _logger.LogWarning("No huts found on page");
                    break;
                }

                foreach (var cabin in response.data)
                {
                    if (cabin.is_delete)
                    {
                        _logger.LogInformation("Skipping deleted cabin with ID {CabinId}", cabin.id);
                        continue;
                    }

                    cabin.id += HutIdOffset;


                    if (cabin.website != null)
                    {
                        if (!cabin.website.StartsWith("http://") && !cabin.website.StartsWith("https://"))
                        {
                            cabin.website = "https://" + cabin.website;
                        }
                    }

                    var hut = await dbContext.Huts.SingleOrDefaultAsync(h => h.Id == cabin.id);

                    if (hut == null)
                    {
                        _logger.LogInformation("Creating new hut object for cabin with ID {CabinId}", cabin.id);

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
                            Country = cabin.country.name.de,
                            Enabled = true,
                            Added = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                        };
                        dbContext.Add(hut);
                    }
                    else
                    {
                        if (hut.ManuallyEdited == true)
                        {
                            _logger.LogInformation("Hut with ID {HutId} was manually edited. Not updating hut information", hut.Id);
                            continue; // Skip updating manually edited huts
                        }

                        // Update existing hut
                        _logger.LogInformation("Updating existing hut with ID {HutId}", hut.Id);
                        hut.Name = cabin.name;
                        hut.HutWebsite = cabin.website;
                        hut.Link = $"https://www.huetten-holiday.com/huts/{cabin.slug}";
                        hut.Latitude = cabin.latitude;
                        hut.Longitude = cabin.longitude;
                        hut.Altitude = (int?)cabin.altitude;
                        hut.Country = cabin.country.name.de;
                        hut.Enabled = true;
                        hut.LastUpdated = DateTime.UtcNow;
                    }

                    huts.Add(hut);
                }

                await dbContext.SaveChangesAsync();

                url = response.next_page_url ?? null;
            } while (url != null);

            _logger.LogInformation("Fetched {HutCount} huts from HuettenHoliday", huts.Count);
            return huts;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching huts from HuettenHoliday");
            return null;
        }
    }
}