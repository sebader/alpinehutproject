using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace WebsiteBackendFunctions
{
    public static class GetAllHutsFunction
    {

        [FunctionName(nameof(GetAllHuts))]
        public static IActionResult GetAllHuts(
                [HttpTrigger(AuthorizationLevel.Function, "get", Route = "hut")] HttpRequest req,
                [Sql("SELECT * FROM [dbo].[Huts]",
            CommandType = System.Data.CommandType.Text,
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<Hut> result,
                ILogger log)
        {
            log.LogInformation("Retrieved {count} huts from database", result?.Count());

            return new OkObjectResult(result);
        }
    }
}
