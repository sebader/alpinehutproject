using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using WebsiteBackendFunctions.Models;

namespace WebsiteBackendFunctions
{
    public static class GetAllAvailabilityOnDateFunction
    {
        [FunctionName(nameof(GetAllAvailabilityOnDate))]
        public static IActionResult GetAllAvailabilityOnDate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "availability/{datefilter:regex(^\\d{{4}}-\\d{{2}}-\\d{{2}}$)}")] HttpRequest req,
            [Sql("SELECT a.hutid as HutId, a.AvailabilityId as AvailabilityId, " +
            "CASE WHEN bc.name IS NOT NULL THEN bc.name ELSE b.name END as BedCategory, " +
            "a.Date as Date, a.FreeRoom as FreeRoom, a.TotalRoom as TotalRoom, a.LastUpdated as LastUpdated " +
            "FROM dbo.Availability a " +
            "JOIN dbo.BedCategories b on a.BedCategoryId = b.id " +
            "LEFT OUTER JOIN dbo.BedCategories bc on b.SharesNameWithBedCateogryId = bc.id " +
            "WHERE a.Date = @DateFilter ",
            CommandType = CommandType.Text,
            Parameters = "@DateFilter={datefilter}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<AvailabilityIntermediaryModel> availabilityResult,
            string dateFilter,
            ILogger log)
        {
            var date = DateTime.Parse(dateFilter);

            // Map database response into nested object, grouped by hut id
            var res = availabilityResult.GroupBy(a => a.HutId).Select(a => new AvailabilityViewModel()
            {
                HutId = a.Key,
                Date = date,
                LastUpdated = a.FirstOrDefault()?.LastUpdated,
                TotalFreeBeds = a.Sum(r => r.FreeRoom),
                TotalBeds = a.Sum(r => r.TotalRoom),
                HutClosed = a.Sum(r => r.TotalRoom) == 0,
                RoomAvailabilities = a.Where(r => r.TotalRoom > 0).Select(r => new RoomAvailabilityViewModel() // Filter out "Hütte Geschlossen (0/0)
                {
                    BedCategory = r.BedCategory,
                    FreeBeds = (int)r.FreeRoom,
                    TotalBeds = (int)r.TotalRoom
                }).ToList()
            }).ToList();

            return new OkObjectResult(res);
        }
    }
}
