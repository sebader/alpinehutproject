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
    public class GetAllHutsFunction
    {
        private readonly ILogger<GetAllHutsFunction> _logger;

        public GetAllHutsFunction(ILogger<GetAllHutsFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetAllHuts))]
        public IActionResult GetAllHuts(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "huts")]
            HttpRequest req,
            [SqlInput("SELECT * FROM [dbo].[Huts]",
                "DatabaseConnectionString",
                CommandType.Text, "")]
            IEnumerable<Hut> result)
        {
            _logger.LogInformation("Retrieved {count} huts from database", result?.Count());
            return new OkObjectResult(result);
        }
    }
}