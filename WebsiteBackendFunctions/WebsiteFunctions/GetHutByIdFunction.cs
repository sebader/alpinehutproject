using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using WebsiteBackendFunctions.Models;

namespace WebsiteBackendFunctions
{
    public class GetHutByIdFunction
    {
        private readonly ILogger<GetHutByIdFunction> _logger;

        public GetHutByIdFunction(ILogger<GetHutByIdFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetHut))]
        public IActionResult GetHut(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "huts/{hutid:int}")]
            HttpRequest req,
            [SqlInput("SELECT * FROM [dbo].[Huts] WHERE id = @Id",
                "DatabaseConnectionString",
                CommandType.Text,
                "@Id={hutid}")]
            IEnumerable<Hut> result,
            int hutId)
        {
            var hut = result?.FirstOrDefault();
            if (hut == null)
            {
                _logger.LogInformation("Not hut found for id {hutId}", hutId);
                return new NotFoundResult();
            }

            _logger.LogInformation("Retrieved hut {hutName} for hut id {hutId}", hut.Name, hutId);
            req.HttpContext.Response.Headers.Append("cache-control", Utils.CacheControlHeader);
            return new OkObjectResult(hut);
        }
    }
}