using System.Data;
using System.Globalization;
using System.Net.Http.Json;
using FetchDataFunctions.Functions;
using FetchDataFunctions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;

namespace FetchDataFunctions;

public class UpdateAvailabilityFunctions(
    IHttpClientFactory clientFactory,
    IDbContextFactory<AlpinehutsDbContext> dbContextFactory,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider,
    ILogger<UpdateAvailabilityFunctions> logger)
{
    // Preferred order of languages for the bed category display name. German first to stay consistent with
    // the historically curated BedCategories names; then fall back through the other provider languages.
    private static readonly string[] BedCategoryLanguagePreference =
        ["DE_DE", "DE_AT", "DE_CH", "DE", "EN", "IT", "FR"];

    [Function(nameof(UpdateAvailabilityTimerTriggered))]
    public async Task UpdateAvailabilityTimerTriggered(
        [TimerTrigger("%AvailabilityUpdateSchedule%")] TimerInfo myTimer,
        [SqlInput("SELECT Id FROM [dbo].[Huts] WHERE Enabled = 1 and Source = 'AV'",
            "DatabaseConnectionString",
            CommandType.Text, "")]
        IEnumerable<Hut> huts,
        [DurableClient] DurableTaskClient starter)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return;
        }

        logger.LogInformation("{FunctionName} function executed", nameof(UpdateAvailabilityTimerTriggered));

        string instanceId =
            await starter.ScheduleNewOrchestrationInstanceAsync(nameof(UpdateAvailabilityOrchestrator), huts.Select(h => h.Id).ToList());
        logger.LogInformation("{OrchestratorName} started. Instance ID={InstanceId}", nameof(UpdateAvailabilityOrchestrator), instanceId);
    }

    /// <summary>
    /// This function can update the availability for one or more huts. More for debugging.
    /// </summary>
    [Function(nameof(UpdateAvailabilityHttpTriggered))]
    public async Task<IActionResult> UpdateAvailabilityHttpTriggered(
        [HttpTrigger(AuthorizationLevel.Function, "get")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Update Hut HTTP trigger function received a request");

        string? hutIds = req.Query["hutid"];
        if (string.IsNullOrEmpty(hutIds))
        {
            return new BadRequestObjectResult("Please pass a comma-separated list of hutid(s) in the query string");
        }

        logger.LogInformation("Request to update huts={HutIds}", hutIds);

        int numRowsWritten = 0;

        foreach (var hutId in hutIds.Split(','))
        {
            if (!int.TryParse(hutId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId))
            {
                logger.LogWarning("Could not parse '{HutId}'. Ignoring", hutId);
                continue;
            }

            var result = await UpdateHutAvailability(parsedId, cancellationToken);
            numRowsWritten += result.NumberOfRowsWritten;
        }

        return new OkObjectResult(numRowsWritten);
    }

    [Function(nameof(UpdateAvailabilityOrchestrator))]
    public async Task UpdateAvailabilityOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var orchestratorLogger = context.CreateReplaySafeLogger<UpdateAvailabilityFunctions>();

        var hutIds = context.GetInput<List<int>>();
        orchestratorLogger.LogInformation("Starting orchestrator with {Count} hut IDs", hutIds!.Count);

        await context.FanOutInBatchesAsync<UpdateAvailabilityResult>(
            hutIds, nameof(UpdateHutAvailability), orchestratorLogger);

        // Call stored proc to update reporting table
        await context.CallActivityAsync(nameof(UpdateAvailabilityReporting),
            new object()); // using new object instead of null to satisfy analyzer warning

        orchestratorLogger.LogInformation("Update availability orchestrator finished");
    }

    [Function(nameof(UpdateHutAvailability))]
    public async Task<UpdateAvailabilityResult> UpdateHutAvailability([ActivityTrigger] int hutId, CancellationToken cancellationToken)
    {
        var result = new UpdateAvailabilityResult
        {
            Messages = []
        };
        try
        {
            logger.LogInformation("Executing UpdateHutAvailability for hutid={HutId}", hutId);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var hut = await dbContext.Huts.AsNoTracking().SingleOrDefaultAsync(h => h.Id == hutId, cancellationToken);
            if (hut == null)
            {
                logger.LogError("No hut found for id={HutId}", hutId);
                return result;
            }

            if (hut.Enabled != true)
            {
                logger.LogError("Hut id={HutId} is not enabled", hutId);
                return result;
            }

            if (hut.Source != "AV")
            {
                logger.LogError("Hut id={HutId} is not from AV source", hutId);
                return result;
            }

            var httpClient = clientFactory.CreateClient("HttpClient");
            // Call the base availability endpoint for the hut once to get a cookie which we then need for the query.
            var availabilityResponse = await httpClient.GetAsync(string.Format(CultureInfo.InvariantCulture, Helpers.GetHutAvailabilityUrlV2, hutId), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!availabilityResponse.IsSuccessStatusCode)
            {
                logger.LogError("Could not get availability for hutid={HutId}", hutId);
                return result;
            }

            var availability = await availabilityResponse.Content.ReadFromJsonAsync<IEnumerable<AvailabilityData>>(ScraperJson.Options, cancellationToken);
            if (availability == null)
            {
                logger.LogError("Could not parse availability for hutid={HutId}", hutId);
                return result;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;

            // Filter out availability entries that are more than 9 months in the future
            var scrapedDays = availability.Where(a => a.DateNormalized < now.AddMonths(9)).ToList();

            var url = string.Format(CultureInfo.InvariantCulture, Helpers.GetHutInfosUrlV2, hutId);
            var hutInfoResponse = await httpClient.GetAsync(url, cancellationToken);
            if (!hutInfoResponse.IsSuccessStatusCode)
            {
                logger.LogError("Could not get hut info for hutid={HutId}", hutId);
                return result;
            }

            var hutInfo = await hutInfoResponse.Content.ReadFromJsonAsync<HutInfoV2>(ScraperJson.Options, cancellationToken);
            if (hutInfo == null)
            {
                logger.LogError("Could not parse hut info for hutid={HutId}", hutId);
                return result;
            }

            // Make sure the BedCategories lookup table contains a row for every tenant bed category of
            // this hut. Availability rows join to it via TenantBedCategoryId; a missing entry makes the
            // website hide the hut's availability entirely (see UpdateAvailabilityReporting / website JOINs).
            await EnsureBedCategoriesExist(hutInfo, cancellationToken);

            // Load all existing availability for this hut once (tracked) and reconcile in memory instead of
            // issuing a query per day/category.
            var existing = await dbContext.Availability.Where(a => a.Hutid == hutId).ToListAsync(cancellationToken);

            var changes = AvailabilityReconciler.Reconcile(hutId, existing, scrapedDays, hutInfo, now, logger);

            dbContext.Availability.AddRange(changes.ToAdd);
            dbContext.Availability.RemoveRange(changes.ToDelete);
            // changes.ToUpdate holds already-mutated tracked entities.

            result.NumberOfRowsWritten += await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e)
        {
            logger.LogError(e, "DbUpdateException in writing availability updates to database for hutid={HutId}", hutId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in getting availability updates from website for hutid={HutId}", hutId);
        }

        return result;
    }

    /// <summary>
    /// Function calls UpdateAvailabilityReporting stored procedure in the database to write
    /// new reporting rows for this day.
    /// </summary>
    /// <param name="input">Not used, but an activity function needs to have this parameter.</param>
    [Function(nameof(UpdateAvailabilityReporting))]
    public async Task UpdateAvailabilityReporting([ActivityTrigger] object input, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Executing stored procedure to update availability reporting");
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync("dbo.UpdateAvailabilityReporting", cancellationToken);
            logger.LogInformation("Inserted {RowsAffected} new rows into reporting table", rowsAffected);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Exception in executing stored procedure to update availability reporting");
        }
    }

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
    private async Task EnsureBedCategoriesExist(HutInfoV2 hutInfo, CancellationToken cancellationToken)
    {
        try
        {
            var neededIds = hutInfo.hutBedCategories.Select(b => b.tenantBedCategoryId).Distinct().ToList();
            if (neededIds.Count == 0)
            {
                return;
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await dbContext.BedCategories.AsNoTracking().ToListAsync(cancellationToken);
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
                logger.LogInformation(
                    "Adding missing BedCategory id={Id} name='{Name}' sharesNameWith={Shares}", tenantId, name, sharesNameWith);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e)
        {
            // A duplicate-key error most likely means a parallel activity (the crawler fans out in batches
            // of 10) inserted the same category first, which is safe to ignore. Any other database error is
            // a real problem and must stay visible in the logs.
            if (e.InnerException is SqlException { Number: 2627 or 2601 })
            {
                logger.LogWarning(e, "Bed category was already inserted by a concurrent activity; ignoring");
            }
            else
            {
                logger.LogError(e, "Database error while inserting missing bed categories");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error ensuring bed categories exist");
        }
    }
}

public class UpdateAvailabilityResult
{
    public int NumberOfRowsWritten { get; set; }
    [SendGridOutput] public SendGridMessage[] Messages { get; set; } = [];
}
