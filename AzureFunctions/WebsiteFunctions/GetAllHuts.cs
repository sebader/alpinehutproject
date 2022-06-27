using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Company.Function
{
    public static class GetAllHutsFunction
    {

    [FunctionName(nameof(GetAllHuts))]
         public static IActionResult GetAllHuts(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Huts]",
            CommandType = System.Data.CommandType.Text,
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<Hut> result,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger with SQL Input Binding function processed a request.");

            return new OkObjectResult(result);
        }
    }
}
