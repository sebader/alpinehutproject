using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FetchDataFunctions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;

namespace FetchDataFunctions
{
    public class UpdateAvailabilityFunctions
    {
        private readonly ILogger<UpdateAvailabilityFunctions> _logger;
        private readonly IHttpClientFactory _clientFactory;

        public UpdateAvailabilityFunctions(IHttpClientFactory clientFactory,
            ILogger<UpdateAvailabilityFunctions> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        [Function(nameof(UpdateAvailabilityTimerTriggered))]
        public async Task UpdateAvailabilityTimerTriggered(
            [TimerTrigger("0 0 14,23 * * *")] TimerInfo myTimer,
            [SqlInput("SELECT Id FROM [dbo].[Huts] WHERE Enabled = 1 and Source = 'AV'",
                "DatabaseConnectionString",
                CommandType.Text, "")]
            IEnumerable<Hut> huts,
            [DurableClient] DurableTaskClient starter)
        {
            if (Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
            {
                return;
            }

            _logger.LogInformation($"{nameof(UpdateAvailabilityTimerTriggered)} function executed at: {DateTime.Now}");

            string instanceId =
                await starter.ScheduleNewOrchestrationInstanceAsync(nameof(UpdateAvailabilityOrchestrator), huts.Select(h => h.Id).ToList());
            _logger.LogInformation($"{nameof(UpdateAvailabilityOrchestrator)} started. Instance ID={instanceId}");
        }

        /// <summary>
        /// This function can update the availability for a single hut. More for debugging.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [Function(nameof(UpdateAvailabilityHttpTriggered))]
        public async Task<IActionResult> UpdateAvailabilityHttpTriggered(
            [HttpTrigger(AuthorizationLevel.Function, "get")]
            HttpRequest req)
        {
            _logger.LogInformation("Update Hut HTTP trigger function received a request");

            string? hutIds = req.Query["hutid"];
            if (string.IsNullOrEmpty(hutIds))
            {
                return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
            }

            _logger.LogInformation("Request to update huts={hutIds}", hutIds);

            int numRowsWritten = 0;

            foreach (var hutId in hutIds.Split(','))
            {
                if (!int.TryParse(hutId, out int parsedId))
                {
                    _logger.LogWarning("Could not parse '{hutId}'. Ignoring", hutId);
                }
                else
                {
                    var result = await UpdateHutAvailability(parsedId);
                    numRowsWritten += result.NumberOfRowsWritten;
                }
            }

            return new OkObjectResult(numRowsWritten);
        }

        [Function(nameof(UpdateAvailabilityOrchestrator))]
        public async Task UpdateAvailabilityOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var orchestratorLogger = context.CreateReplaySafeLogger<UpdateAvailabilityFunctions>();

            var hutIds = context.GetInput<List<int>>();

            orchestratorLogger.LogInformation($"Starting orchestrator with {hutIds!.Count} hut IDs");

            var tasks = new List<Task>();

            // Fan-out
            foreach (var hutId in hutIds)
            {
                orchestratorLogger.LogInformation("Starting UpdateHutAvailability Activity Function for hutId={hutId}", hutId);
                tasks.Add(context.CallActivityAsync<UpdateAvailabilityResult>(nameof(UpdateHutAvailability), hutId));

                // In order not to run into rate limiting, we process in batches of 10 and then wait for 1 minute
                if (tasks.Count >= 10)
                {
                    orchestratorLogger.LogInformation("Delaying next batch for 1 minute, last hutId={hutid}", hutId);
                    await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(1), CancellationToken.None);

                    orchestratorLogger.LogInformation(
                        "Waiting for batch to finishing UpdateHutAvailability Activity Functions");
                    // Fan-in (wait for all tasks to be completed)
                    await Task.WhenAll(tasks);
                    orchestratorLogger.LogInformation("Finished batch");

                    tasks.Clear();
                }
            }

            orchestratorLogger.LogInformation(
                "All UpdateHutAvailability Activity Functions scheduled. Waiting for finishing last batch");

            // Fan-in (wait for all tasks to be completed)
            await Task.WhenAll(tasks);

            // Call stored proc to update reporting table
            await context.CallActivityAsync(nameof(UpdateAvailabilityReporting),
                new object()); // using new object instead of null to satisfy analyzer warning

            orchestratorLogger.LogInformation($"Update availability orchestrator finished");
        }

        [Function(nameof(UpdateHutAvailability))]
        public async Task<UpdateAvailabilityResult> UpdateHutAvailability([ActivityTrigger] int hutId)
        {
            var result = new UpdateAvailabilityResult
            {
                Messages = []
            };
            try
            {
                _logger.LogInformation("Executing UpdateHutAvailability for hutid={hutId}", hutId);
                var dbContext = Helpers.GetDbContext();

                var hut = await dbContext.Huts.Include(h => h.Availability).ThenInclude(a => a.BedCategory)
                    .AsNoTracking().SingleOrDefaultAsync(h => h.Id == hutId);
                if (hut == null)
                {
                    _logger.LogError("No hut found for id={hutId}", hutId);
                    return result;
                }

                if (hut.Enabled != true)
                {
                    _logger.LogError("Hut id={hutId} is not enabled", hutId);
                    return result;
                }

                if (hut.Source != "AV")
                {
                    _logger.LogError("Hut id={hutId} is not from AV source", hutId);
                    return result;
                }

                var httpClient = _clientFactory.CreateClient("HttpClient");
                // Call the base page for the hut once to get a cookie which we then need for the selectDate query. We only need to do a HEAD request
                var availabilityResponse = await httpClient.GetAsync(string.Format(Helpers.GetHutAvailabilityUrlV2, hutId), HttpCompletionOption.ResponseHeadersRead);
                if (!availabilityResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Could not get availability for hutid={hutId}", hutId);
                    return result;
                }

                var availability = await availabilityResponse.Content.ReadFromJsonAsync<IEnumerable<AvailabilityData>>();
                if (availability == null)
                {
                    _logger.LogError("Could not parse availability for hutid={hutId}", hutId);
                    return result;
                }

                // Filter out availability entries that are more than 9 months in the future
                availability = availability.Where(a => a.DateNormalized < DateTime.UtcNow.AddMonths(9));

                var updateTime = DateTime.UtcNow;

                var url = string.Format(Helpers.GetHutInfosUrlV2, hutId);
                var hutInfoResponse = await httpClient.GetAsync(url);
                if (!hutInfoResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Could not get hut info for hutid={hutId}", hutId);
                    return result;
                }

                var hutInfo = await hutInfoResponse.Content.ReadFromJsonAsync<HutInfoV2>();
                if (hutInfo == null)
                {
                    _logger.LogError("Could not parse hut info for hutid={hutId}", hutId);
                    return result;
                }

                // Make sure the BedCategories lookup table contains a row for every tenant bed category of
                // this hut. Availability rows join to it via TenantBedCategoryId; a missing entry makes the
                // website hide the hut's availability entirely (see UpdateAvailabilityReporting / website JOINs).
                await EnsureBedCategoriesExist(hutInfo);

                foreach (var day in availability)
                {
                    if (day.HutClosed)
                    {
                        // See if there is any existing availability entries that we should delete
                        var existingAva = await dbContext.Availability.Where(a =>
                            a.Hutid == hutId && a.Date == day.DateNormalized &&
                            a.BedCategoryId != BedCategory.HutClosedBedcatoryId).ToListAsync();
                        foreach (var obsoleteAva in existingAva)
                        {
                            dbContext.Remove(obsoleteAva);
                        }

                        var existingCloseAva = await dbContext.Availability.FirstOrDefaultAsync(a =>
                            a.Hutid == hutId && a.Date == day.DateNormalized &&
                            a.BedCategoryId == BedCategory.HutClosedBedcatoryId);
                        if (existingCloseAva == null)
                        {
                            var newAva = new Availability
                            {
                                BedCategoryId = BedCategory.HutClosedBedcatoryId,
                                Date = day.DateNormalized,
                                FreeRoom = 0,
                                TotalRoom = 0,
                                Hutid = hutId,
                                LastUpdated = updateTime
                            };
                            _logger.LogDebug(
                                $"Adding new 'Closed' availability for hutid={hutId} date={newAva.Date} bedCategoryId={newAva.BedCategoryId}");
                            dbContext.Availability.Add(newAva);
                        }
                    }
                    else
                    {
                        foreach (var (bedCategory, freeBeds) in day.freeBedsPerCategory)
                        {
                            var bedCategoryId = int.Parse(bedCategory);

                            var matchingBedCategory = FindMatchingBedCategory(hutInfo, bedCategoryId, day.hutStatus);
                            if (matchingBedCategory == null)
                            {
                                if (matchingBedCategory == null)
                                {
                                    _logger.LogDebug("Could not find matching bed category for hutid={hutId} and bedCategory={bedCategory}", hutId, bedCategoryId);
                                    continue;
                                }
                            }

                            var existingAva = await dbContext.Availability.FirstOrDefaultAsync(a =>
                                a.Hutid == hutId && a.Date == day.DateNormalized &&
                                a.BedCategoryId == bedCategoryId);
                            if (existingAva != null)
                            {
                                existingAva.FreeRoom = freeBeds;
                                existingAva.TotalRoom = matchingBedCategory.totalSleepingPlaces;
                                existingAva.LastUpdated = updateTime;
                                existingAva.TenantBedCategoryId = matchingBedCategory.tenantBedCategoryId;
                                _logger.LogDebug(
                                    $"Updating existing availability for hutid={hutId} date={day.DateNormalized} bedCategoryId={bedCategoryId} FreeRoom={freeBeds}");
                                dbContext.Update(existingAva);
                            }
                            else
                            {
                                var newAva = new Availability
                                {
                                    BedCategoryId = bedCategoryId,
                                    TenantBedCategoryId = matchingBedCategory.tenantBedCategoryId,
                                    Date = day.DateNormalized,
                                    FreeRoom = freeBeds,
                                    TotalRoom = matchingBedCategory.totalSleepingPlaces,
                                    Hutid = hutId,
                                    LastUpdated = updateTime
                                };
                                _logger.LogDebug($"Adding new availability for hutid={hutId} date={newAva.Date} bedCategoryId={newAva.BedCategoryId}");
                                dbContext.Availability.Add(newAva);
                            }

                            var allBedcategories = day.freeBedsPerCategory.Select(r => int.Parse(r.Key)).ToList();
                            var oldEntries = await dbContext.Availability.Where(a =>
                                a.Hutid == hutId && a.Date == day.DateNormalized &&
                                !allBedcategories.Contains(a.BedCategoryId)).ToListAsync();
                            if (oldEntries.Count > 0)
                            {
                                _logger.LogInformation("Found {count} orphaned availability entries for hut={hutid} date={date}", oldEntries.Count, hutId, day.DateNormalized);
                                foreach (var entry in oldEntries)
                                {
                                    _logger.LogInformation("Deleting entry with bedCategoryId={bed}", entry.BedCategoryId);
                                    dbContext.Availability.Remove(entry);
                                }
                            }
                        }
                    }
                }

                result.NumberOfRowsWritten += await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                _logger.LogError(default, e, "DbUpdateException in writing availability updates to database for hutid={hutId}", hutId);
            }
            catch (Exception e)
            {
                _logger.LogError(default, e, "Exception in getting availability updates from website for hutid={hutId}", hutId);
            }

            return result;
        }

        /// <summary>
        /// Function calls UpdateAvailabilityReporting stored procedure in the database to write 
        /// new reporting rows for this day
        /// </summary>
        /// <param name="input">Not used, but an activity function needs to have this parameter</param>
        /// <returns></returns>
        [Function(nameof(UpdateAvailabilityReporting))]
        public async Task UpdateAvailabilityReporting([ActivityTrigger] object input)
        {
            try
            {
                _logger.LogInformation("Executing stored procedure to update availability reporting");
                var dbContext = Helpers.GetDbContext();
                var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync("dbo.UpdateAvailabilityReporting");
                _logger.LogInformation("Inserted {rowsAffected} new rows into reporting table", rowsAffected);
            }
            catch (Exception e)
            {
                _logger.LogError(default, e,
                    "Exception in executing stored procedure to update availability reporting");
            }
        }

        private static HutBedCategory? FindMatchingBedCategory(HutInfoV2 hutInfo, int bedCategoryId, string hutStatus)
        {
            // Matching pairs:
            // - ReservationMode = "ROOM" and hutStatus = "SERVICED"
            // - ReservationMode = "UNSERVICED" and hutStatus = "UNSERVICED"

            var matchingBedCategory = hutInfo.hutBedCategories.FirstOrDefault(b => b.categoryID == bedCategoryId &&
                                                                                   (b.reservationMode == "ROOM" && hutStatus == "SERVICED" || b.reservationMode == "UNSERVICED" && hutStatus == "UNSERVICED"));

            return matchingBedCategory;
        }

        // Preferred order of languages for the bed category display name. German first to stay consistent with
        // the historically curated BedCategories names; then fall back through the other provider languages.
        private static readonly string[] BedCategoryLanguagePreference =
            ["DE_DE", "DE_AT", "DE_CH", "DE", "EN", "IT", "FR"];

        private static string GetBedCategoryName(HutBedCategory cat)
        {
            foreach (var pref in BedCategoryLanguagePreference)
            {
                var match = cat.hutBedCategoryLanguageData.FirstOrDefault(l =>
                    string.Equals(l.language, pref, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(l.label));
                if (match != null)
                {
                    return match.label.Trim();
                }
            }

            var any = cat.hutBedCategoryLanguageData.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.label));
            return any?.label.Trim() ?? $"Kategorie {cat.tenantBedCategoryId}";
        }

        /// <summary>
        /// Ensures the BedCategories lookup table contains a row (keyed by tenantBedCategoryId) for every bed
        /// category of the given hut. New categories are named from the provider label (German preferred) and,
        /// when the name exactly matches an existing category, linked to that category's canonical entry via
        /// SharesNameWithBedCateogryId so no duplicate display name is introduced.
        /// </summary>
        private async Task EnsureBedCategoriesExist(HutInfoV2 hutInfo)
        {
            try
            {
                var neededIds = hutInfo.hutBedCategories.Select(b => b.tenantBedCategoryId).Distinct().ToList();
                if (neededIds.Count == 0)
                {
                    return;
                }

                var dbContext = Helpers.GetDbContext();
                var existing = await dbContext.BedCategories.AsNoTracking().ToListAsync();
                var existingIds = existing.Select(b => b.Id).ToHashSet();

                var missing = neededIds.Where(id => !existingIds.Contains(id)).ToList();
                if (missing.Count == 0)
                {
                    return;
                }

                // Map existing (curated) category name -> its canonical id, used to link duplicates by name.
                var nameToCanonical = existing
                    .Where(b => !string.IsNullOrWhiteSpace(b.Name))
                    .GroupBy(b => b.Name!.Trim().ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().SharesNameWithBedCateogryId ?? g.First().Id);

                foreach (var tenantId in missing)
                {
                    var cat = hutInfo.hutBedCategories.First(b => b.tenantBedCategoryId == tenantId);
                    var name = GetBedCategoryName(cat);
                    int? sharesNameWith =
                        nameToCanonical.TryGetValue(name.ToLowerInvariant(), out var canonicalId) ? canonicalId : null;

                    dbContext.BedCategories.Add(new BedCategory
                    {
                        Id = tenantId,
                        Name = name,
                        SharesNameWithBedCateogryId = sharesNameWith
                    });
                    _logger.LogInformation(
                        "Adding missing BedCategory id={id} name='{name}' sharesNameWith={shares}", tenantId, name, sharesNameWith);
                }

                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                // A duplicate-key error most likely means a parallel activity (the crawler fans out in batches
                // of 10) inserted the same category first, which is safe to ignore. Any other database error is
                // a real problem and must stay visible in the logs.
                if (e.InnerException is SqlException { Number: 2627 or 2601 })
                {
                    _logger.LogWarning(e, "Bed category was already inserted by a concurrent activity; ignoring");
                }
                else
                {
                    _logger.LogError(default, e, "Database error while inserting missing bed categories");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(default, e, "Unexpected error ensuring bed categories exist");
            }
        }
    }

    public class UpdateAvailabilityResult
    {
        public int NumberOfRowsWritten { get; set; }
        [SendGridOutput] public SendGridMessage[] Messages { get; set; } = [];
    }
}