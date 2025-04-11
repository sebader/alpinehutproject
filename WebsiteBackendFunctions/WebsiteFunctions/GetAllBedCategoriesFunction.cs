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
    public class GetAllBedCategoriesFunction
    {
        private readonly ILogger<GetAllBedCategoriesFunction> _logger;

        public GetAllBedCategoriesFunction(ILogger<GetAllBedCategoriesFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetAllBedCategories))]
        public IActionResult GetAllBedCategories(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "bedcategories")]
            HttpRequest req,
            [SqlInput(
                "SELECT DISTINCT name FROM [dbo].[BedCategories] WHERE SharesNameWithBedCateogryId IS NULL AND Id <> -1",
                "DatabaseConnectionString",
                CommandType.Text, "")]
            IEnumerable<BedCategoryViewModel> result)
        {
            _logger.LogInformation("Retrieved {count} bed categories from database", result?.Count());
            req.HttpContext.Response.Headers.Append("cache-control", Utils.CacheControlHeader);
            return new OkObjectResult(result);
        }
    }
}