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
    public static class GetHutByIdFunction
    {
        [FunctionName(nameof(GetHut))]
        public static IActionResult GetHut(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hut/{hutid:int}")] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Huts] WHERE id = @Id",
            "DatabaseConnectionString",
            CommandType.Text,
            "@Id={hutid}")] IEnumerable<Hut> result,
            int hutId,
            ILogger log)
        {

            if (result == null || !result.Any())
            {
                log.LogInformation("Not hut found for id {hutid}", hutId);
                return new NotFoundResult();
            }

            var hut = result.FirstOrDefault();
            log.LogInformation("Retrieved hut {hutName} for id {hutid}", hut?.Name, hutId);
            return new OkObjectResult(hut);
        }
    }
}
