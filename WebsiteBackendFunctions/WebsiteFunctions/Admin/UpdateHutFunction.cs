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
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace WebsiteBackendFunctions.WebsiteFunctions.Admin
{
    public class UpdateHutFunction
    {
        private readonly ILogger<UpdateHutFunction> _logger;

        public UpdateHutFunction(ILogger<UpdateHutFunction> logger)
        {
            _logger = logger;
        }
        private static readonly JsonSerializerOptions JsonSerializerOptions =
            new() { PropertyNameCaseInsensitive = true };

        [Function(nameof(UpdateHut))]
        public async Task<HutOutput> UpdateHut(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "huts/{id}")]
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
                _logger.LogWarning("Hut with id {id} not found", id);
                return new HutOutput(new NotFoundObjectResult(new { message = "Hut not found" }));
            }

            Hut? requestBody;
            
            try {
                var requestBodyString = await new StreamReader(req.Body).ReadToEndAsync();
                requestBody = JsonSerializer.Deserialize<Hut>(requestBodyString, JsonSerializerOptions);
                
                if (requestBody == null)
                {
                    _logger.LogWarning("Request body could not be deserialized");
                    return new HutOutput(new BadRequestObjectResult(new { message = "Invalid request body format" }));
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing request body");
                return new HutOutput(new BadRequestObjectResult(new { message = "Error parsing request data: " + ex.Message }));
            }

            if (requestBody.Id != id)
            {
                return new HutOutput(new BadRequestObjectResult(new { message = "Invalid request body or ID mismatch" }));
            }

            try
            {
                requestBody.Added = hut.Added;
                requestBody.Activated = hut.Activated;
                requestBody.Source = hut.Source; // Preserve the source field
                requestBody.LastUpdated = DateTime.UtcNow;
                requestBody.ManuallyEdited = true;
                _logger.LogInformation("Updating hut with id {id}", id);
                
                return new HutOutput(new OkObjectResult(requestBody), requestBody); // Pass requestBody as UpdatedHut to trigger SQL output binding
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating hut with id {id}", id);
                return new HutOutput(new StatusCodeResult(StatusCodes.Status500InternalServerError));
            }
        }
    }
    
    public class HutOutput
    {
        [SqlOutput("dbo.Huts", connectionStringSetting: "DatabaseConnectionString")]
        public Hut? UpdatedHut { get; set; }
        
        [HttpResult]
        public IActionResult HttpResponse { get; set; }

        public HutOutput(IActionResult response, Hut? updatedHut = null)
        {
            HttpResponse = response;
            UpdatedHut = updatedHut;
        }
    }
}
