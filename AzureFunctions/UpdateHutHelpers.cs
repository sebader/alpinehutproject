using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AzureFunctions
{
    public static class UpdateHutHelpers
    {
        public static Hut ParseHutInformation(string httpBody, ILogger log)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(httpBody);

            var infoDiv = doc.DocumentNode.SelectSingleNode("//body").Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "info").FirstOrDefault();
            if (infoDiv != null)
            {
                string hutName = infoDiv.ChildNodes["h4"].InnerText;
                var spans = infoDiv.ChildNodes.Where(c => c.Name == "span").ToArray();
                string phoneNumber = spans[1]?.InnerText;
                log.LogDebug($"Phonenumber={phoneNumber}");
                string coordinates = spans[4]?.InnerText;

                coordinates = Regex.Replace(coordinates, @"\s+", " ");
                coordinates = Regex.Replace(coordinates, "Koordinaten: ", "");

                bool hutEnabled = !httpBody.Contains("Diese Hütte ist nicht freigeschaltet");

                string country = GetCountry(hutName, phoneNumber, httpBody);

                Hut hut = new Hut()
                {
                    Name = hutName,
                    Enabled = hutEnabled,
                    Coordinates = coordinates,
                    Country = country,
                    LastUpdated = DateTime.Now
                };

                log.LogInformation($"Hut info parsed: name='{hutName}' country='{country}' hutEnabled='{hutEnabled}' coordinates='{coordinates}'");

                return hut;
            }

            log.LogError("Please pass valid hut html page in the request body");
            return null;
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
