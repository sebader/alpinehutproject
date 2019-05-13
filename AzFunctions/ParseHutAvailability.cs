using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using AlpineHutsProject.Model;
using System.Collections.Generic;
using System;
using System.Globalization;

namespace AlpineHutsProject
{
    public static class ParseHutAvailability
    {
        [FunctionName("ParseHutAvailability")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, ILogger log)
        {
            log.LogInformation("Availability parsing request received");
            string requestBody = new StreamReader(req.Body).ReadToEnd();

            try
            {
                JObject json = JsonConvert.DeserializeObject<JObject>(requestBody);

                string hutLanguage = "";
                var resultList = new List<Day>();

                foreach (var day in json.Children())
                {
                    var parsedDay = new Day();
                    foreach (var room in day.First?.Children())
                    {
                        var roomDayAvailabilty = JsonConvert.DeserializeObject<RoomDayAvailability>(room.ToString());
                        if (!roomDayAvailabilty.Closed)
                        {
                            var a = new RoomAvailability
                            {
                                BedCategoryId = roomDayAvailabilty.BedCategoryId,
                                FreeRoom = roomDayAvailabilty.FreeRoom,
                                TotalRoom = roomDayAvailabilty.TotalRoom
                            };
                            parsedDay.Rooms.Add(a);

                            if (parsedDay.Date == null)
                            {
                                parsedDay.Date = DateTime.ParseExact(roomDayAvailabilty.ReservationDate, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                            }
                        }

                        if (hutLanguage == "")
                        {
                            hutLanguage = roomDayAvailabilty.HutDefaultLanguage;
                        }

                    }
                    if (parsedDay.Date != null)
                    {
                        resultList.Add(parsedDay);
                    }
                }

                var res = new
                {
                    hutLanguage,
                    availability = resultList
                };

                log.LogInformation($"Parsed availability for {resultList.Count} entries");

                return (ActionResult)new OkObjectResult(res);
            }
            catch (Exception e)
            {
                log.LogError($"Exception occurred: {e.Message}", e);
                return new BadRequestObjectResult($"Exception occurred: {e.Message}");
            }
        }
    }
}
