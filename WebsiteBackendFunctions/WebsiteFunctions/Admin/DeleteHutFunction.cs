using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using WebsiteBackendFunctions.Models;

namespace WebsiteBackendFunctions.WebsiteFunctions.Admin
{
    public class DeleteHutFunction
    {
        private readonly ILogger<DeleteHutFunction> _logger;

        public DeleteHutFunction(ILogger<DeleteHutFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(DeleteHut))]
        public IActionResult DeleteHut(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "huts/{id}")]
            HttpRequest req,
            [SqlInput(commandText: "DeleteHutById", commandType: CommandType.StoredProcedure,
                parameters: "@HutId={id}", connectionStringSetting: "DatabaseConnectionString")]
            IEnumerable<Hut> huts,
            int id)
        {
            _logger.LogInformation("DeleteHut function processed a request for hut id {id}", id);
            return new OkObjectResult(new { message = "Hut deleted successfully" });
        }
    }
}