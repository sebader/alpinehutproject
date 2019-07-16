using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AzureFunctions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models;

namespace AzureFunctions
{
    public static class UpdateAvailabilityFunctions
    {
        [FunctionName("UpdateAvailabilityTimerTriggered")]
        public static async Task UpdateAvailabilityTimerTriggered([TimerTrigger("0 0 23 * * *")]TimerInfo myTimer,
            ILogger log,
            [OrchestrationClient] DurableOrchestrationClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }

            log.LogInformation($"UpdateAvailabilityFunctions Timer trigger function executed at: {DateTime.Now}");

            var dbContext = await Helpers.GetDbContext();
            var hutIds = dbContext.Huts.Where(h => h.Enabled == true).Select(h => h.Id).ToList();

            string instanceId = await starter.StartNewAsync("UpdateAvailabilityOrchestrator", hutIds);
            log.LogInformation($"UpdateAvailability orchestrator started. Instance ID={instanceId}");
        }

        /// <summary>
        /// This function can update the availability for a single hut. More for debugging.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("UpdateAvailabilityHttpTriggered")]
        public static async Task<IActionResult> UpdateAvailabilityHttpTriggered(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("Update Hut HTTP trigger function received a request");

            string hutIds = req.Query["hutid"];
            if (string.IsNullOrEmpty(hutIds))
            {
                return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
            }

            int numRowsWritten = 0;

            foreach (var hutId in hutIds.Split(','))
            {
                int parsedId;
                if (!int.TryParse(hutId, out parsedId))
                {
                    log.LogWarning($"Could not parse '{hutId}'. Ignoring");
                }

                numRowsWritten += await UpdateHutAvailability(parsedId, log);
            }
            return new OkObjectResult(numRowsWritten);
        }

        [FunctionName("UpdateAvailabilityOrchestrator")]
        public static async Task UpdateAvailabilityOrchestrator(
           [OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var hutIds = context.GetInput<List<int>>();
            if (!context.IsReplaying)
            {
                log.LogInformation($"Starting orchestrator with {hutIds.Count} hut IDs");
            }

            var tasks = new Task[hutIds.Count];

            // Fan-out
            for (int i = 0; i < hutIds.Count; i++)
            {
                tasks[i] = context.CallActivityAsync("UpdateHutAvailability", hutIds[i]);
            }

            await Task.WhenAll(tasks);

            // Call stored proc to update reporting table
            await context.CallActivityAsync("UpdateAvailabilityReporting", null);

            if (!context.IsReplaying)
            {
                log.LogInformation($"Update availability orchestrator finished");
            }
        }

        [FunctionName("UpdateHutAvailability")]
        public static async Task<int> UpdateHutAvailability([ActivityTrigger] int hutId, ILogger log)
        {
            int numRowsWritten = 0;
            try
            {
                log.LogInformation($"Executing UpdateHutAvailability for hutid={hutId}");
                var dbContext = await Helpers.GetDbContext();

                var hut = await dbContext.Huts.Where(h => h.Id == hutId).Include(h => h.Availability).ThenInclude(a => a.BedCategory).SingleOrDefaultAsync();
                if (hut == null || hut.Enabled != true)
                {
                    log.LogError($"No hut found for id={hutId} or hut not enabled");
                    return numRowsWritten;
                }

                using (HttpClient httpClient = new HttpClient())
                {
                    // Call the base page for the hut once to get a cookie which we then need for the selectDate query
                    var initialResponse = await httpClient.GetAsync(hut.Link);

                    var cookie = initialResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
                    if (!string.IsNullOrEmpty(cookie))
                    {
                        httpClient.DefaultRequestHeaders.Add("Cookie", cookie);

                        var startDate = DateTime.UtcNow;
                        // Each selectDate query gives a 14 day window, so we increment by 14
                        // 112 = 16 weeks
                        for (int dateOffset = 0; dateOffset < 112; dateOffset += 14)
                        {
                            var date = startDate.AddDays(dateOffset).ToString("dd.MM.yyyy");
                            var dateUrl = $"{Helpers.SelectDateBaseUrl}{date}";
                            log.LogDebug($"Calling selectDate for hutid={hutId} and date={date} ({dateUrl})");
                            var dateResponse = await httpClient.GetStringAsync(dateUrl);
                            var updateTime = DateTime.UtcNow;

                            var daysAvailability = ParseAvailability(dateResponse);

                            foreach (var day in daysAvailability)
                            {
                                foreach (var room in day.Rooms)
                                {
                                    var existingAva = await dbContext.Availability.FirstOrDefaultAsync(a => a.Hutid == hutId && a.Date == day.Date && a.BedCategoryId == room.BedCategoryId);
                                    if (existingAva != null)
                                    {
                                        existingAva.FreeRoom = room.FreeRoom;
                                        existingAva.TotalRoom = room.TotalRoom;
                                        existingAva.LastUpdated = updateTime;
                                        log.LogDebug($"Updating existing availability for hutid={hutId} date={day.Date} bedCategoryId={room.BedCategoryId} FreeRoom={room.FreeRoom}");
                                        dbContext.Update(existingAva);
                                    }
                                    else
                                    {
                                        var newAva = new Availability()
                                        {
                                            BedCategoryId = (int)room.BedCategoryId,
                                            Date = (DateTime)day.Date,
                                            FreeRoom = room.FreeRoom,
                                            TotalRoom = room.TotalRoom,
                                            Hutid = hutId,
                                            LastUpdated = updateTime
                                        };
                                        log.LogDebug($"Adding new availability for hutid={hutId} date={newAva.Date} bedCategoryId={newAva.BedCategoryId}");
                                        dbContext.Availability.Add(newAva);
                                    }
                                }
                            }
                            numRowsWritten += await dbContext.SaveChangesAsync();
                        }
                        log.LogInformation($"Finished updating availability for hutId={hut.Id} ({hut.Name})");
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in writing availability updates to database for hutid=" + hutId);
            }
            return numRowsWritten;
        }

        private static List<DayAvailability> ParseAvailability(string requestBody)
        {
            JObject json = JsonConvert.DeserializeObject<JObject>(requestBody);

            var resultList = new List<DayAvailability>();

            foreach (var day in json.Children())
            {
                var parsedDay = new DayAvailability();
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
                }
                if (parsedDay.Date != null)
                {
                    resultList.Add(parsedDay);
                }
            }
            return resultList;
        }

        /// <summary>
        /// Function calls UpdateAvailabilityReporting stored procedure in the database to write 
        /// new reporting rows for this day
        /// </summary>
        /// <param name="input"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("UpdateAvailabilityReporting")]
        public static async Task UpdateAvailabilityReporting([ActivityTrigger] object input, ILogger log)
        {
            try
            {
                log.LogInformation("Executing stored procedure to update availability reporting");
                var dbContext = await Helpers.GetDbContext();
                var rowsAffected = await dbContext.Database.ExecuteSqlCommandAsync("dbo.UpdateAvailabilityReporting");
                log.LogInformation($"Inserted {rowsAffected} new rows into reporting table");
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in executing stored procedure to update availability reporting");
            }
        }
    }
}
