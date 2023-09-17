using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebsiteBackendFunctions.Models;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace WebsiteBackendFunctions.WebsiteFunctions
{
    public static class CreateFreeBedNotificationSubscription
    {
        [FunctionName(nameof(CreateFreeBedNotificationSubscription))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "freebednotification/{hutId:int}")] HttpRequest req,
            [Sql("SELECT * FROM [dbo].[Huts] WHERE id = @Id",
            "DatabaseConnectionString",
            CommandType.Text,
            "@Id={hutId}")] IEnumerable<Hut> result,
            int hutId,
            [Sql(commandText: "dbo.FreeBedUpdateSubscriptions", connectionStringSetting: "DatabaseConnectionString")] IAsyncCollector<FreeBedUpdateSubscription> newItems,
            ILogger log)
        {
            log.LogInformation("New request to create a free bed notification");

            var hut = result.FirstOrDefault();
            if (hut == null)
            {
                return new BadRequestObjectResult("Hut does not exist");
            }

            if (hut.Enabled == false)
            {
                return new BadRequestObjectResult("Hut is not enabled");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var body = JsonSerializer.Deserialize<FreeBedUpdateSubscription>(requestBody, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

            ICollection<ValidationResult> results = null;
            if (!Validate(body, out results))
            {
                return new BadRequestObjectResult(results.Select(o => o.ErrorMessage));
            }

            if (body.Date < DateTime.Today)
            {
                return new BadRequestObjectResult("Date must not be in the past");
            }

            body.HutId = hutId;
            body.Notified = false;

            log.LogInformation("Adding subscription to the database");
            await newItems.AddAsync(body);

            return new OkResult();
        }
        private static bool Validate<T>(T obj, out ICollection<ValidationResult> results)
        {
            results = new List<ValidationResult>();

            return Validator.TryValidateObject(obj, new ValidationContext(obj), results, true);
        }
    }
}
