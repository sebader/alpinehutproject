using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace AzureFunctions.WebsiteFunctions
{
    public static class GetHutByIdFunction
    {
        [FunctionName(nameof(GetHut))]
        public static IActionResult GetHut(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Huts] WHERE id = @Id",
            CommandType = System.Data.CommandType.Text,
            Parameters = "@Id={Query.id}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<Hut> result,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger with SQL Input Binding function processed a request.");

            if(result?.Count() == 0)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(result.FirstOrDefault());
        }
}
}
