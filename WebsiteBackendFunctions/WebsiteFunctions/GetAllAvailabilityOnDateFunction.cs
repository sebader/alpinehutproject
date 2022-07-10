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
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [Sql("SELECT h.id as HutId, a.AvailabilityId as AvailabilityId, b.Name as BedCategory, a.Date as Date, a.FreeRoom as FreeRoom, a.TotalRoom as TotalRoom, a.LastUpdated as LastUpdated " +
            "FROM dbo.Availability a " +
            "JOIN dbo.Huts h on a.hutid = h.id " +
            "JOIN dbo.BedCategories b on a.BedCategoryId = b.id " +
            "WHERE a.Date = @DateFilter",
            CommandType = CommandType.Text,
            Parameters = "@DateFilter={Query.dateFilter}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<AvailabilityViewModel> availabilityResult,
            [Sql("SELECT * FROM [dbo].[Huts]",
            CommandType = CommandType.Text,
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<Hut> hutsResult,
            ILogger log)
        {
            var date = DateTime.Parse(req.Query["dateFilter"]);
            var result = new List<MapViewModel>();
            foreach (var hut in hutsResult)
            {
                var hutAvailabilities = (bool)hut.Enabled ? availabilityResult.Where(a => a.Hutid == hut.Id).ToList() : null;
                var mvm = new MapViewModel
                {
                    Hutid = hut.Id,
                    Availabilities = hutAvailabilities,
                    HutEnabled = (bool)hut.Enabled,
                    HutName = hut.Name,
                    HutWebsite = hut.HutWebsite,
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
                        a.Hutid = null;
                    }
                }

                result.Add(mvm);
            }

            return new OkObjectResult(result);
        }
    }
}
