using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text.Json;
using Alpinehuts.Shared.ViewModels;

namespace WebsiteBackendFunctions
{
    public static class GetAllAvailabilityOnDateFunction
    {
        [FunctionName(nameof(GetAllAvailabilityOnDate))]
        public static IActionResult GetAllAvailabilityOnDate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "availability/{datefilter:regex(^\\d{{4}}-\\d{{2}}-\\d{{2}}$)}")] HttpRequest req,
            [Sql("SELECT h.id as HutId, a.AvailabilityId as AvailabilityId, " +
            "CASE WHEN bc.name IS NOT NULL THEN bc.name ELSE b.name END as BedCategory, " +
            "a.Date as Date, a.FreeRoom as FreeRoom, a.TotalRoom as TotalRoom, a.LastUpdated as LastUpdated " +
            "FROM dbo.Availability a " +
            "JOIN dbo.Huts h on a.hutid = h.id " +
            "JOIN dbo.BedCategories b on a.BedCategoryId = b.id " +
            "LEFT OUTER JOIN dbo.BedCategories bc on b.SharesNameWithBedCateogryId = bc.id " +
            "WHERE a.Date = @DateFilter ",
            CommandType = CommandType.Text,
            Parameters = "@DateFilter={datefilter}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<AvailabilityIntermediaryModel> availabilityResult,
            [Sql("SELECT * FROM [dbo].[Huts] h WHERE h.latitude IS NOT NULL AND h.longitude IS NOT NULL",
            CommandType = CommandType.Text,
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<Hut> hutsResult,
            string dateFilter,
            ILogger log)
        {
            var date = DateTime.Parse(dateFilter);
            var result = new List<MapViewModel>();
            foreach (var hut in hutsResult)
            {
                var hutAvailabilities = (bool)hut.Enabled ? availabilityResult.Where(a => a.HutId == hut.Id).ToList() : null;
                var mvm = new MapViewModel
                {
                    HutId = hut.Id,
                    Availabilities = hutAvailabilities,
                    HutEnabled = (bool)hut.Enabled,
                    HutName = hut.Name,
                    HutWebsite = hut.HutWebsite,
                    BookingLink = hut.Link,
                    FreeBeds = hutAvailabilities?.Sum(a => a.FreeRoom),
                    LastUpdated = hutAvailabilities?.FirstOrDefault()?.LastUpdated,
                    Latitude = hut.Latitude,
                    Longitude = hut.Longitude,
                    Date = date
                };

                if(hutAvailabilities != null)
                {
                    foreach(var a in hutAvailabilities)
                    {
                        a.AvailabilityId = null;
                        a.LastUpdated = null;
                        a.Date = null;
                        a.HutId = null;
                    }
                }

                result.Add(mvm);
            }
/*
            return new JsonResult(result, new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
*/
            return new OkObjectResult(result);
        }
    }
}
