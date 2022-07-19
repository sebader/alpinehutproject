using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Alpinehuts.Shared.ViewModels;
using System;

namespace WebsiteBackendFunctions
{
    public static class GetHutAvailabilityFunction
    {
        [FunctionName(nameof(GetHutAvailability))]
        public static IActionResult GetHutAvailability(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hut/{hutid:int}/availability")] HttpRequest req,
            [Sql("SELECT a.hutId as HutId, a.AvailabilityId as AvailabilityId, " +
            "CASE WHEN bc.name IS NOT NULL THEN bc.name ELSE b.name END as BedCategory, " +
            "a.Date as Date, a.FreeRoom as FreeRoom, a.TotalRoom as TotalRoom, a.LastUpdated as LastUpdated " +
            "FROM dbo.Availability a " +
            "JOIN dbo.BedCategories b on a.BedCategoryId = b.id " +
            "LEFT OUTER JOIN dbo.BedCategories bc on b.SharesNameWithBedCateogryId = bc.id " +
            "WHERE a.hutid = @HutId AND a.Date >= CURRENT_TIMESTAMP " +
            "ORDER BY a.date ASC",
            CommandType = CommandType.Text,
            Parameters = "@HutId={hutid}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<AvailabilityIntermediaryModel> availabilityResult,
            int hutId,
            ILogger log)
        {
            if (availabilityResult == null || availabilityResult.Count() == 0)
            {
                log.LogInformation("Not availability found for id {hutid}", hutId);
                return new NotFoundResult();
            }

            var result = new List<AvailabilityViewModel>();
            foreach (var a in availabilityResult.GroupBy(a => a.Date))
            {
                var avw = new AvailabilityViewModel
                {
                    HutId = a.FirstOrDefault()?.HutId,
                    Date = (DateTime)a.Key,
                    LastUpdated = a.FirstOrDefault()?.LastUpdated,
                    TotalFreeBeds = a.Sum(r => r.FreeRoom),
                    RoomAvailabilities = a.Select(a => new RoomAvailabilityViewModel()
                    {
                        BedCategory = a.BedCategory,
                        FreeBeds = (int)a.FreeRoom,
                        TotalBeds = (int)a.TotalRoom
                    }).ToList()
                };
                result.Add(avw);
            }

            log.LogInformation("Retrieved {count} availabilites for hut id {hutid}", result.Count(), hutId);
            return new OkObjectResult(result);
        }
    }
}
