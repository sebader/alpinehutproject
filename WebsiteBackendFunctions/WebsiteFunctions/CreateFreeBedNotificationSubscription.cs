using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebsiteBackendFunctions.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker.Http;

namespace WebsiteBackendFunctions.WebsiteFunctions
{
    public class CreateFreeBedNotificationSubscription
    {
        private readonly ILogger<CreateFreeBedNotificationSubscription> _logger;

        private static readonly JsonSerializerOptions JsonSerializerOptions =
            new() { PropertyNameCaseInsensitive = true };

        public CreateFreeBedNotificationSubscription(ILogger<CreateFreeBedNotificationSubscription> logger)
        {
            _logger = logger;
        }

        [Function(nameof(CreateFreeBedNotificationSubscription))]
        public async Task<OutputType> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "freebednotifications/{hutId:int}")]
            HttpRequestData req,
            [SqlInput("SELECT * FROM [dbo].[Huts] WHERE id = @Id",
                "DatabaseConnectionString",
                CommandType.Text,
                "@Id={hutId}")]
            IEnumerable<Hut> huts,
            int hutId)
        {
            _logger.LogInformation("New request to create a free bed notification");

            var output = new OutputType();

            var hut = huts.FirstOrDefault();
            if (hut == null)
            {
                output.HttpResponse = new BadRequestObjectResult("Hut does not exist");
                return output;
            }

            if (hut.Enabled == false)
            {
                output.HttpResponse = new BadRequestObjectResult("Hut is not enabled");
                return output;
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var body = JsonSerializer.Deserialize<FreeBedUpdateSubscription>(requestBody, JsonSerializerOptions);

            if (!Validate(body, out var results))
            {
                output.HttpResponse = new BadRequestObjectResult(results.Select(o => o.ErrorMessage));
                return output;
            }

            if (body.Date < DateTime.Today)
            {
                output.HttpResponse = new BadRequestObjectResult("Date must not be in the past");
                return output;
            }

            if (body.Date > DateTime.Today.AddDays(90))
            {
                output.HttpResponse = new BadRequestObjectResult("Date must not be more than 90 days in the future");
                return output;
            }

            body.HutId = hutId;
            body.Notified = false;

            _logger.LogInformation("Adding subscription to the database for hut {hutId}, date {date}", hutId,
                body.Date);

            output.Item = body;
            output.HttpResponse = new OkResult();

            return output;
        }

        private static bool Validate<T>(T obj, out ICollection<ValidationResult> results)
        {
            results = new List<ValidationResult>();

            return Validator.TryValidateObject(obj, new ValidationContext(obj), results, true);
        }

        public class OutputType
        {
            [SqlOutput(commandText: "dbo.FreeBedUpdateSubscriptions",
                connectionStringSetting: "DatabaseConnectionString")]
            public FreeBedUpdateSubscription Item { get; set; }

            [HttpResult] public IActionResult HttpResponse { get; set; }
        }
    }
}