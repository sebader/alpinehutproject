using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using WebsiteBackendFunctions.Models;

namespace WebsiteBackendFunctions
{
    public static class GetAllBedCategoriesFunction
    {
        [FunctionName(nameof(GetAllBedCategories))]
        public static IActionResult GetAllBedCategories(
                [HttpTrigger(AuthorizationLevel.Function, "get", Route = "bedcategory")] HttpRequest req,
                [Sql("SELECT DISTINCT name FROM [dbo].[BedCategories] WHERE SharesNameWithBedCateogryId IS NULL AND Id <> -1",
                    "DatabaseConnectionString",
                    CommandType.Text)] IEnumerable<BedCategoryViewModel> result,
                ILogger log)
        {
            log.LogInformation("Retrieved {count} bed categories from database", result?.Count());

            return new OkObjectResult(result);
        }
    }
}
