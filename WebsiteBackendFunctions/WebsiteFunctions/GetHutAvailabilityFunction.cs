using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Alpinehuts.Shared.ViewModels;

namespace WebsiteBackendFunctions
{
    public static class GetHutAvailabilityFunction
    {
        [FunctionName(nameof(GetHutAvailability))]
        public static IActionResult GetHutAvailability(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [Sql("SELECT h.id as HutId, a.AvailabilityId as AvailabilityId, b.Name as BedCategory, a.Date as Date, a.FreeRoom as FreeRoom, a.TotalRoom as TotalRoom, a.LastUpdated as LastUpdated " +
            "FROM dbo.Availability a " +
            "JOIN dbo.Huts h on a.hutid = h.id " +
            "JOIN dbo.BedCategories b on a.BedCategoryId = b.id " +
            "WHERE a.hutid = @HutId AND a.Date >= CURRENT_TIMESTAMP",
            CommandType = CommandType.Text,
            Parameters = "@HutId={Query.hutid}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<AvailabilityViewModel> availabilityResult,
            ILogger log)
        {

            if (availabilityResult == null || availabilityResult.Count() == 0)
            {
                log.LogInformation("Not availability found for id {hutid}", req.Query["hutid"]);
                return new NotFoundResult();
            }

            log.LogInformation("Retrieved {count} availabilites for hut id {hutid}", availabilityResult.Count(), req.Query["hutid"]);
            return new OkObjectResult(availabilityResult);
        }
    }
}
