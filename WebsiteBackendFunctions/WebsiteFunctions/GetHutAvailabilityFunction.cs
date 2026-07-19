using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using WebsiteBackendFunctions.Models;

namespace WebsiteBackendFunctions
{
    public class GetHutAvailabilityFunction
    {
        private readonly ILogger<GetHutAvailabilityFunction> _logger;

        public GetHutAvailabilityFunction(ILogger<GetHutAvailabilityFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetHutAvailability))]
        public IActionResult GetHutAvailability(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "huts/{hutid:int}/availability")]
            HttpRequest req,
            [SqlInput("SELECT a.hutId as HutId, a.AvailabilityId as AvailabilityId, " +
                      "COALESCE(bc.name, b.name, N'Unbekannt') as BedCategory, " +
                      "a.Date as Date, a.FreeRoom as FreeRoom, a.TotalRoom as TotalRoom, a.LastUpdated as LastUpdated " +
                      "FROM dbo.Availability a " +
                      // LEFT JOIN so availability is still returned when the tenant bed category is missing
                      // from the BedCategories lookup table (otherwise the whole hut shows no availability).
                      "LEFT OUTER JOIN dbo.BedCategories b ON a.TenantBedCategoryId = b.id " +
                      "LEFT OUTER JOIN dbo.BedCategories bc ON b.SharesNameWithBedCateogryId = bc.id " +
                      "WHERE a.hutid = @HutId AND a.Date >= CONVERT (date, GETDATE()) AND a.TenantBedCategoryId IS NOT NULL " +
                      "ORDER BY a.date ASC, BedCategory ASC",
                "DatabaseConnectionString",
                CommandType.Text,
                "@HutId={hutid}"
            )]
            IEnumerable<AvailabilityIntermediaryModel> availabilityResult,
            int hutId)
        {
            var result = new List<AvailabilityViewModel>();
            foreach (var a in availabilityResult.GroupBy(a => a.Date))
            {
                var avw = new AvailabilityViewModel
                {
                    HutId = a.FirstOrDefault()?.HutId,
                    Date = (DateTime)a.Key,
                    LastUpdated = a.FirstOrDefault()?.LastUpdated,
                    TotalFreeBeds = a.Sum(r => r.FreeRoom),
                    TotalBeds = a.Sum(r => r.TotalRoom),
                    HutClosed = a.Sum(r => r.TotalRoom) == 0,
                    RoomAvailabilities = a.Where(r => r.TotalRoom > 0).Select(a =>
                        new RoomAvailabilityViewModel() // Filter out "H�tte Geschlossen (0/0)
                        {
                            BedCategory = a.BedCategory,
                            FreeBeds = (int)a.FreeRoom,
                            TotalBeds = (int)a.TotalRoom
                        }).ToList()
                };
                result.Add(avw);
            }

            _logger.LogInformation("Retrieved {count} availabilities for hut id {hutId}", result.Count, hutId);
            req.HttpContext.Response.Headers.Append("cache-control", Utils.CacheControlHeader);
            return new OkObjectResult(result);
        }
    }
}