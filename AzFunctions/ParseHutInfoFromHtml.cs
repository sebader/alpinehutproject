
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using System.Linq;
using System.Text.RegularExpressions;
using System;

namespace AlpineHutsProject
{
    public static class ParseHutInfoFromHtml
    {
        [FunctionName("ParseHutInfoFromHtml")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, ILogger log)
        {
            log.LogInformation("Hut Info parsing request received");
            string requestBody = new StreamReader(req.Body).ReadToEnd();

            var doc = new HtmlDocument();
            doc.LoadHtml(requestBody);

            var infoDiv = doc.DocumentNode.Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "info").FirstOrDefault();
            if (infoDiv != null)
            {
                string hutName = infoDiv.ChildNodes["h4"].InnerText;
                var spans = infoDiv.ChildNodes.Where(c => c.Name == "span").ToArray();
                string phoneNumber = spans[1]?.InnerText;
                log.LogInformation($"Phonenumber={phoneNumber}");
                string coordinates = spans[4]?.InnerText;

                coordinates = Regex.Replace(coordinates, @"\s+", " ");
                coordinates = Regex.Replace(coordinates, "Koordinaten: ", "");

                bool hutEnabled = !requestBody.Contains("Diese Hütte ist nicht freigeschaltet");

                string country = GetCountry(hutName, phoneNumber, requestBody);

                dynamic result = new
                {
                    hutName,
                    hutEnabled,
                    coordinates,
                    country
                };

                log.LogInformation($"Hut info parsed: name='{hutName}' country='{country}' hutEnabled='{hutEnabled}' coordinates='{coordinates}'");

                return (ActionResult)new OkObjectResult(result);
            }
            return new BadRequestObjectResult("Please pass valid hut html page in the request body");
        }

        private static string GetCountry(string hutName, string phoneNumber, string requestBody)
        {
            string[] swissNames = { "SAC", "CAS", "AACZ" };
            if (swissNames.Any(n => hutName.Contains(n) || phoneNumber.Contains("+41") || phoneNumber.Contains("0041")))
            {
                return "Switzerland";
            }

            string[] southTyrolNames = { "AVS" };
            if (southTyrolNames.Any(n => hutName.Contains(n) || phoneNumber.Contains("+39") || phoneNumber.Contains("0039")))
            {
                return "Italy";
            }

            if (phoneNumber.Contains("+43") || phoneNumber.Contains("0043"))
            {
                return "Austria";
            }

            if (phoneNumber.Contains("+49") || phoneNumber.Contains("0049"))
            {
                return "Germany";
            }

            if (requestBody.Contains("de_CH"))
            {
                return "Switzerland";
            }
            else if (requestBody.Contains("de_AT"))
            {
                return "Austria";
            }
            else if (requestBody.Contains("de_DE"))
            {
                return "Germany/Austria";
            }

            return "Germany/Austria";
        }
    }
}
