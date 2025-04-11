using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        [SqlOutput("DELETE FROM Huts WHERE Id = @Id", "DatabaseConnectionString")]
        public IActionResult DeleteHut(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "huts/{id}")]
            HttpRequest req,
            [SqlInput("SELECT * FROM Huts WHERE Id = @Id",
                "DatabaseConnectionString",
                CommandType.Text,
                parameters: "@Id={id}")]
            IEnumerable<Hut> existingHuts,
            int id)
        {
            var hut = existingHuts.FirstOrDefault();
            if (hut == null)
            {
                _logger.LogWarning("Hut with id {id} not found for deletion", id);
                return new NotFoundObjectResult(new { message = "Hut not found" });
            }

            try
            {
                _logger.LogInformation("Deleting hut with id {id}", id);
                return new OkObjectResult(new { message = "Hut deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting hut with id {id}", id);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
