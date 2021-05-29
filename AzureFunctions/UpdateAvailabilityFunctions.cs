using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AzureFunctions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models;

namespace AzureFunctions
{
    public class UpdateAvailabilityFunctions
    {
        private IHttpClientFactory _clientFactory;
        public UpdateAvailabilityFunctions(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [FunctionName(nameof(UpdateAvailabilityTimerTriggered))]
        public async Task UpdateAvailabilityTimerTriggered([TimerTrigger("0 0 14,23 * * *")]TimerInfo myTimer,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }

            log.LogInformation($"{nameof(UpdateAvailabilityTimerTriggered)} function executed at: {DateTime.Now}");

            var dbContext = await Helpers.GetDbContext();
            // Fetch all hut ids which are in Enabled state from database
            var hutIds = await dbContext.Huts.Where(h => h.Enabled == true).AsNoTracking().Select(h => h.Id).ToListAsync();

            string instanceId = await starter.StartNewAsync(nameof(UpdateAvailabilityOrchestrator), hutIds);
            log.LogInformation($"{nameof(UpdateAvailabilityOrchestrator)} started. Instance ID={instanceId}");
        }

        /// <summary>
        /// This function can update the availability for a single hut. More for debugging.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName(nameof(UpdateAvailabilityHttpTriggered))]
        public async Task<IActionResult> UpdateAvailabilityHttpTriggered(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("Update Hut HTTP trigger function received a request");

            string hutIds = req.Query["hutid"];
            if (string.IsNullOrEmpty(hutIds))
            {
                return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
            }

            log.LogInformation("Request to update huts={hutIds}", hutIds);

            int numRowsWritten = 0;

            foreach (var hutId in hutIds.Split(','))
            {
                int parsedId;
                if (!int.TryParse(hutId, out parsedId))
                {
                    log.LogWarning("Could not parse '{hutId}'. Ignoring", hutId);
                }
                else
                {
                    numRowsWritten += await UpdateHutAvailability(parsedId, log);
                }
            }
            return new OkObjectResult(numRowsWritten);
        }

        [FunctionName(nameof(UpdateAvailabilityOrchestrator))]
        public async Task UpdateAvailabilityOrchestrator(
           [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            log = context.CreateReplaySafeLogger(log);

            var hutIds = context.GetInput<List<int>>();

            log.LogInformation($"Starting orchestrator with {hutIds.Count} hut IDs");

            var tasks = new List<Task>();

            // Fan-out
            foreach (var hutId in hutIds)
            {
                log.LogInformation("Starting UpdateHutAvailability Activity Function for hutId={hutId}", hutId);
                tasks.Add(context.CallActivityAsync(nameof(UpdateHutAvailability), hutId));

                // In order not to run into rate limiting, we process in batches of 10 and then wait for 1 minute
                if(tasks.Count >= 10)
                {
                    log.LogInformation("Delaying next batch for 1 minute, last hutId={hutid}", hutId);
                    await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(1), CancellationToken.None);

                    log.LogInformation("Waiting for batch to finishing UpdateHutAvailability Activity Functions");
                    // Fan-in (wait for all tasks to be completed)
                    await Task.WhenAll(tasks);
                    log.LogInformation("Finished batch");

                    tasks.Clear();
                }
            }

            log.LogInformation("All UpdateHutAvailability Activity Functions scheduled. Waiting for finishing last batch");

            // Fan-in (wait for all tasks to be completed)
            await Task.WhenAll(tasks);

            // Call stored proc to update reporting table
            await context.CallActivityAsync(nameof(UpdateAvailabilityReporting), new object()); // using new object instead of null to satisfy analyzer warning

            log.LogInformation($"Update availability orchestrator finished");
        }

        [FunctionName(nameof(UpdateHutAvailability))]
        public async Task<int> UpdateHutAvailability([ActivityTrigger] int hutId, ILogger log)
        {
            int numRowsWritten = 0;
            try
            {
                log.LogInformation("Executing UpdateHutAvailability for hutid={hutId}", hutId);
                var dbContext = await Helpers.GetDbContext();

                var hut = await dbContext.Huts.Include(h => h.Availability).ThenInclude(a => a.BedCategory).AsNoTracking().SingleOrDefaultAsync(h => h.Id == hutId);
                if (hut == null)
                {
                    log.LogError("No hut found for id={hutId}", hutId);
                    return numRowsWritten;
                }
                else if (hut.Enabled != true)
                {
                    log.LogError("Hut id={hutId} is not enabled", hutId);
                    return numRowsWritten;
                }

                var httpClient = _clientFactory.CreateClient("HttpClient");
                // Call the base page for the hut once to get a cookie which we then need for the selectDate query. We only need to do a HEAD request
                var initialResponse = await httpClient.GetAsync(hut.Link, HttpCompletionOption.ResponseHeadersRead);

                var cookie = initialResponse.Headers.GetValues("Set-Cookie").Where(c => c.StartsWith("JSESSIONID")).FirstOrDefault();
                if (!string.IsNullOrEmpty(cookie))
                {
                    httpClient.DefaultRequestHeaders.Add("Cookie", cookie);

                    var startDate = DateTime.UtcNow;
                    // Each selectDate query gives a 14 day window, so we increment by 14
                    // 112 = 16 weeks in the future
                    for (int dateOffset = 0; dateOffset < 112; dateOffset += 14)
                    {
                        var date = startDate.AddDays(dateOffset).ToString("dd.MM.yyyy");
                        var dateUrl = $"{Helpers.SelectDateBaseUrl}{date}";
                        log.LogDebug($"Calling selectDate for hutid={hutId} and date={date} ({dateUrl})");
                        var dateResponse = await httpClient.GetStringAsync(dateUrl);
                        var updateTime = DateTime.UtcNow;

                        var daysAvailability = ParseAvailability(dateResponse, log);

                        log.LogInformation("Found {numberOfDay} days with availability for hut={hutId} starting with date={date}. Writing to database now", daysAvailability.Count, hutId, date);

                        foreach (var day in daysAvailability)
                        {
                            foreach (var room in day.Rooms)
                            {
                                var existingAva = await dbContext.Availability.FirstOrDefaultAsync(a => a.Hutid == hutId && a.Date == day.Date && a.BedCategoryId == room.BedCategoryId);
                                if (existingAva != null)
                                {
                                    if (room.Closed)
                                    {
                                        // Was not closed before, so we delete the row
                                        dbContext.Remove(existingAva);
                                    }
                                    else
                                    {
                                        existingAva.FreeRoom = room.FreeRoom;
                                        existingAva.TotalRoom = room.TotalRoom;
                                        existingAva.LastUpdated = updateTime;
                                        log.LogDebug($"Updating existing availability for hutid={hutId} date={day.Date} bedCategoryId={room.BedCategoryId} FreeRoom={room.FreeRoom}");
                                        dbContext.Update(existingAva);
                                    }
                                }
                                else
                                {
                                    if (room.Closed)
                                    {
                                        log.LogDebug($"Skipping availability for hutid={hutId} date={day.Date} bedCategoryId={room.BedCategoryId} because closed on that date");
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
                                var allBedcategories = day.Rooms.Select(r => (int) r.BedCategoryId).ToList();
                                var oldEntries = await dbContext.Availability.Where(a => a.Hutid == hutId && a.Date == day.Date && !allBedcategories.Contains(a.BedCategoryId)).ToListAsync();
                                if(oldEntries.Count > 0)
                                {
                                    log.LogInformation("Found {count} orphaned availability entries for hut={hutid} date={date}", oldEntries.Count, hutId, day.Date);
                                    foreach(var entry in oldEntries)
                                    {
                                        log.LogInformation("Deleting entry with bedCategoryId={bed}", entry.BedCategoryId);
                                        dbContext.Availability.Remove(entry);
                                    }
                                }
                            }

                            // If the bed category changes over time, there might be obsolete entries in the database which we remove here
                            var obsoleteExistingAva = await dbContext.Availability.Where(a => a.Hutid == hutId && a.Date == day.Date && !day.Rooms.Select(d => d.BedCategoryId).Contains(a.BedCategoryId)).ToListAsync();
                            foreach (var ava in obsoleteExistingAva)
                            {
                                log.LogInformation("Removing obsolete existing availability for hut {hutid}, on {date} with bedCategoryId {bedCategoryId}", hutId, ava.Date, ava.BedCategoryId);
                            }
                            dbContext.RemoveRange(obsoleteExistingAva);

                            numRowsWritten += await dbContext.SaveChangesAsync();
                        }
                    }
                    log.LogInformation("Finished updating availability for hutId={hutId}. Number of rows written={numberOfRowsWritten}", hut.Id, numRowsWritten);
                }
            }
            catch (DbUpdateException e)
            {
                log.LogError(default, e, "DbUpdateException in writing availability updates to database for hutid={hutId}", hutId);
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in getting availability updates from website for hutid={hutId}", hutId);
            }
            return numRowsWritten;
        }

        private List<DayAvailability> ParseAvailability(string responseBody, ILogger log)
        {
            var jObject = JsonConvert.DeserializeObject<JObject>(responseBody);

            var resultList = new List<DayAvailability>();

            foreach (var day in jObject.Children())
            {
                var parsedDay = new DayAvailability();
                foreach (var room in day.First?.Children())
                {
                    var roomDayAvailabilty = JsonConvert.DeserializeObject<RoomDayAvailability>(room.ToString());
                    if(roomDayAvailabilty.BedCategoryId == null || roomDayAvailabilty.HutBedCategoryId == null)
                    {
                        log.LogWarning("Parsed JSON has BedCategoryId=null or HutBedCategoryId=null. Raw JSON={json}", room.ToString());
                        continue;
                    }

                    var a = new RoomAvailability
                    {
                        BedCategoryId = roomDayAvailabilty.BedCategoryId,
                        FreeRoom = roomDayAvailabilty.FreeRoom,
                        TotalRoom = roomDayAvailabilty.TotalRoom,
                        Closed = roomDayAvailabilty.Closed
                    };
                    parsedDay.Rooms.Add(a);

                    if (parsedDay.Date == null)
                    {
                        parsedDay.Date = DateTime.ParseExact(roomDayAvailabilty.ReservationDate, "dd.MM.yyyy", CultureInfo.InvariantCulture);
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
        /// <param name="input">Not used, but an activity function needs to have this parameter</param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName(nameof(UpdateAvailabilityReporting))]
        public async Task UpdateAvailabilityReporting([ActivityTrigger] object input, ILogger log)
        {
            try
            {
                log.LogInformation("Executing stored procedure to update availability reporting");
                var dbContext = await Helpers.GetDbContext();
                var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync("dbo.UpdateAvailabilityReporting");
                log.LogInformation("Inserted {rowsAffected} new rows into reporting table", rowsAffected);
            }
            catch (Exception e)
            {
                log.LogError(default, e, "Exception in executing stored procedure to update availability reporting");
            }
        }
    }
}
