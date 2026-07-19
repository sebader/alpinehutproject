using System.Globalization;
using FetchDataFunctions.Models;
using Microsoft.Extensions.Logging;

namespace FetchDataFunctions;

/// <summary>
/// The set of database changes needed to make the stored availability for a hut match what the provider
/// currently reports. <see cref="ToUpdate"/> contains the (already mutated) existing rows.
/// </summary>
public sealed class AvailabilityChangeSet
{
    public List<Availability> ToAdd { get; } = [];
    public List<Availability> ToUpdate { get; } = [];
    public List<Availability> ToDelete { get; } = [];
}

/// <summary>
/// Pure, side-effect-free reconciliation of scraped AV availability against the rows already in the database.
/// Kept free of EF/HTTP dependencies so the upsert/close/orphan logic is unit-testable.
/// </summary>
public static class AvailabilityReconciler
{
    public static AvailabilityChangeSet Reconcile(
        int hutId,
        IReadOnlyCollection<Availability> existing,
        IEnumerable<AvailabilityData> scrapedDays,
        HutInfoV2 hutInfo,
        DateTime updateTime,
        ILogger? logger = null)
    {
        var changes = new AvailabilityChangeSet();
        var existingByDate = existing
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var day in scrapedDays)
        {
            var date = day.DateNormalized;
            if (!existingByDate.TryGetValue(date, out var existingForDay))
            {
                existingForDay = [];
            }

            if (day.HutClosed)
            {
                // A closed day is represented by a single sentinel row; drop any real availability for it.
                foreach (var obsolete in existingForDay.Where(a => a.BedCategoryId != BedCategory.HutClosedBedcatoryId))
                {
                    changes.ToDelete.Add(obsolete);
                }

                if (existingForDay.All(a => a.BedCategoryId != BedCategory.HutClosedBedcatoryId))
                {
                    changes.ToAdd.Add(new Availability
                    {
                        BedCategoryId = BedCategory.HutClosedBedcatoryId,
                        Date = date,
                        FreeRoom = 0,
                        TotalRoom = 0,
                        Hutid = hutId,
                        LastUpdated = updateTime
                    });
                }

                continue;
            }

            var scrapedCategoryIds = day.freeBedsPerCategory.Keys
                .Select(k => int.Parse(k, CultureInfo.InvariantCulture))
                .ToHashSet();

            // In the original code the orphan-deletion pass lived inside the per-category loop, so it only
            // ran once a category actually matched. Preserve that: an open day that reports no categories (or
            // only unmatched ones) - e.g. a transient/malformed provider response - must not delete stored rows.
            var anyCategoryMatched = false;

            foreach (var (bedCategoryKey, freeBeds) in day.freeBedsPerCategory)
            {
                var bedCategoryId = int.Parse(bedCategoryKey, CultureInfo.InvariantCulture);

                var matchingBedCategory = FindMatchingBedCategory(hutInfo, bedCategoryId, day.hutStatus);
                if (matchingBedCategory == null)
                {
                    logger?.LogDebug("Could not find matching bed category for hutid={HutId} and bedCategory={BedCategory}", hutId, bedCategoryId);
                    continue;
                }

                anyCategoryMatched = true;

                var existingAva = existingForDay.FirstOrDefault(a => a.BedCategoryId == bedCategoryId);
                if (existingAva != null)
                {
                    existingAva.FreeRoom = freeBeds;
                    existingAva.TotalRoom = matchingBedCategory.totalSleepingPlaces;
                    existingAva.LastUpdated = updateTime;
                    existingAva.TenantBedCategoryId = matchingBedCategory.tenantBedCategoryId;
                    changes.ToUpdate.Add(existingAva);
                }
                else
                {
                    changes.ToAdd.Add(new Availability
                    {
                        BedCategoryId = bedCategoryId,
                        TenantBedCategoryId = matchingBedCategory.tenantBedCategoryId,
                        Date = date,
                        FreeRoom = freeBeds,
                        TotalRoom = matchingBedCategory.totalSleepingPlaces,
                        Hutid = hutId,
                        LastUpdated = updateTime
                    });
                }
            }

            if (!anyCategoryMatched)
            {
                continue;
            }

            // Delete rows for categories the provider no longer reports for this day (including a stale
            // "closed" sentinel when the day is now open).
            foreach (var orphan in existingForDay.Where(a => !scrapedCategoryIds.Contains(a.BedCategoryId)))
            {
                changes.ToDelete.Add(orphan);
            }
        }

        return changes;
    }

    /// <summary>
    /// Finds the bed category from the hut info that matches the scraped category id and reservation mode.
    /// Matching pairs: reservationMode "ROOM" ↔ hutStatus "SERVICED"; "UNSERVICED" ↔ "UNSERVICED".
    /// </summary>
    public static HutBedCategory? FindMatchingBedCategory(HutInfoV2 hutInfo, int bedCategoryId, string hutStatus)
    {
        return hutInfo.hutBedCategories.FirstOrDefault(b => b.categoryID == bedCategoryId &&
            ((b.reservationMode == "ROOM" && hutStatus == "SERVICED") ||
             (b.reservationMode == "UNSERVICED" && hutStatus == "UNSERVICED")));
    }
}
