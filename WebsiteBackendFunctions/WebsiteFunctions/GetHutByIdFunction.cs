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
using System.Data;

namespace WebsiteBackendFunctions
{
    public static class GetHutByIdFunction
    {
        [FunctionName(nameof(GetHut))]
        public static IActionResult GetHut(
            [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Huts] WHERE id = @Id",
            CommandType = CommandType.Text,
            Parameters = "@Id={Query.id}",
            ConnectionStringSetting = "DatabaseConnectionString")] IEnumerable<Hut> result,
            ILogger log)
        {

            if (result == null || result.Count() == 0)
            {
                log.LogInformation("Not hut found for id {hutid}", req.Query["id"]);
                return new NotFoundResult();
            }

            var hut = result.FirstOrDefault();
            log.LogInformation("Retrieved hut {hutName} for id {hutid}", hut?.Name, req.Query["id"]);
            return new OkObjectResult(hut);
        }
    }
}
