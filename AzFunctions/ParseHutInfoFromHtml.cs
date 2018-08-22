
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
            log.LogInformation("Parsing request received");
            string requestBody = new StreamReader(req.Body).ReadToEnd();

            var doc = new HtmlDocument();
            doc.LoadHtml(requestBody);

            var infoDiv = doc.DocumentNode.Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "info").FirstOrDefault();
            if (infoDiv != null)
            {
                string hutName = infoDiv.ChildNodes["h4"].InnerText;
                var spans = infoDiv.ChildNodes.Where(c => c.Name == "span").ToArray();
                string coordinates = spans[4]?.InnerText;

                coordinates = Regex.Replace(coordinates, @"\s+", " ");
                coordinates = Regex.Replace(coordinates, "Koordinaten: ", "");

                bool hutEnabled = !requestBody.Contains("Diese Hütte ist nicht freigeschaltet");

                string country = GetCountry(hutName);

                dynamic result = new
                {
                    hutName,
                    hutEnabled,
                    coordinates,
                    country
                };

                log.LogInformation($"Hut info parsed: name='{hutName}' hutEnabled='{hutEnabled}' coordinates='{coordinates}'");

                return (ActionResult)new OkObjectResult(result);
            }
            return new BadRequestObjectResult("Please pass valid hut html page in the request body");
        }

        private static string GetCountry(string hutName)
        {
            string[] swissNames = {"SAC", "CAS", "AACZ"};
            if(swissNames.Any(n => hutName.Contains(n))) {
                return "Switzerland";
            }
            
            return "DE/A";
        }
    }
}
