using FetchDataFunctions;
using FetchDataFunctions.Models;

namespace FetchDataFunctions.Tests;

[TestFixture]
public class AvailabilityReconcilerTests
{
    private const int HutId = 29;
    private static readonly DateTime UpdateTime = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day = new(2026, 8, 1);

    private static HutInfoV2 HutInfo() => new()
    {
        hutId = HutId,
        hutBedCategories =
        [
            new HutBedCategory { categoryID = 5, tenantBedCategoryId = 1, totalSleepingPlaces = 10, reservationMode = "ROOM" },
            new HutBedCategory { categoryID = 36, tenantBedCategoryId = 2, totalSleepingPlaces = 6, reservationMode = "ROOM" },
            new HutBedCategory { categoryID = 5, tenantBedCategoryId = 1, totalSleepingPlaces = 10, reservationMode = "UNSERVICED" },
        ]
    };

    private static AvailabilityData OpenDay(Dictionary<string, int> freeBeds, string hutStatus = "SERVICED") =>
        new() { date = Day, hutStatus = hutStatus, percentage = "AVAILABLE", freeBedsPerCategory = freeBeds };

    [Test]
    public void NewOpenDay_AddsOneRowPerMatchingCategory()
    {
        var changes = AvailabilityReconciler.Reconcile(
            HutId, [], [OpenDay(new() { ["5"] = 3, ["36"] = 2 })], HutInfo(), UpdateTime);

        Assert.That(changes.ToUpdate, Is.Empty);
        Assert.That(changes.ToDelete, Is.Empty);
        Assert.That(changes.ToAdd, Has.Count.EqualTo(2));

        var cat5 = changes.ToAdd.Single(a => a.BedCategoryId == 5);
        Assert.Multiple(() =>
        {
            Assert.That(cat5.FreeRoom, Is.EqualTo(3));
            Assert.That(cat5.TotalRoom, Is.EqualTo(10));
            Assert.That(cat5.TenantBedCategoryId, Is.EqualTo(1));
            Assert.That(cat5.Hutid, Is.EqualTo(HutId));
            Assert.That(cat5.Date, Is.EqualTo(Day));
            Assert.That(cat5.LastUpdated, Is.EqualTo(UpdateTime));
        });
    }

    [Test]
    public void ExistingOpenDay_UpdatesRowInPlace()
    {
        var existing = new Availability { Hutid = HutId, Date = Day, BedCategoryId = 5, FreeRoom = 1, TotalRoom = 10, TenantBedCategoryId = 1 };

        var changes = AvailabilityReconciler.Reconcile(
            HutId, [existing], [OpenDay(new() { ["5"] = 7 })], HutInfo(), UpdateTime);

        Assert.That(changes.ToAdd, Is.Empty);
        Assert.That(changes.ToDelete, Is.Empty);
        Assert.That(changes.ToUpdate, Has.Count.EqualTo(1));
        Assert.That(changes.ToUpdate[0], Is.SameAs(existing));
        Assert.That(existing.FreeRoom, Is.EqualTo(7));
        Assert.That(existing.LastUpdated, Is.EqualTo(UpdateTime));
    }

    [Test]
    public void ClosedDay_DeletesRealRowsAndAddsSentinel()
    {
        var existing = new Availability { Hutid = HutId, Date = Day, BedCategoryId = 5, FreeRoom = 3 };

        var changes = AvailabilityReconciler.Reconcile(
            HutId, [existing], [OpenDay([], hutStatus: "CLOSED")], HutInfo(), UpdateTime);

        Assert.That(changes.ToDelete, Has.Count.EqualTo(1));
        Assert.That(changes.ToDelete[0], Is.SameAs(existing));
        Assert.That(changes.ToAdd, Has.Count.EqualTo(1));
        Assert.That(changes.ToAdd[0].BedCategoryId, Is.EqualTo(BedCategory.HutClosedBedcatoryId));
        Assert.That(changes.ToAdd[0].FreeRoom, Is.EqualTo(0));
    }

    [Test]
    public void ClosedDay_WithExistingSentinel_IsNoOp()
    {
        var sentinel = new Availability { Hutid = HutId, Date = Day, BedCategoryId = BedCategory.HutClosedBedcatoryId };

        var changes = AvailabilityReconciler.Reconcile(
            HutId, [sentinel], [OpenDay([], hutStatus: "CLOSED")], HutInfo(), UpdateTime);

        Assert.That(changes.ToAdd, Is.Empty);
        Assert.That(changes.ToUpdate, Is.Empty);
        Assert.That(changes.ToDelete, Is.Empty);
    }

    [Test]
    public void OpenDay_DeletesOrphanCategoriesAndStaleSentinel()
    {
        var stillReported = new Availability { Hutid = HutId, Date = Day, BedCategoryId = 5, FreeRoom = 1, TotalRoom = 10 };
        var orphan = new Availability { Hutid = HutId, Date = Day, BedCategoryId = 99, FreeRoom = 4 };
        var staleSentinel = new Availability { Hutid = HutId, Date = Day, BedCategoryId = BedCategory.HutClosedBedcatoryId };

        var changes = AvailabilityReconciler.Reconcile(
            HutId, [stillReported, orphan, staleSentinel], [OpenDay(new() { ["5"] = 2 })], HutInfo(), UpdateTime);

        Assert.That(changes.ToUpdate, Has.Count.EqualTo(1));
        Assert.That(changes.ToDelete, Has.Count.EqualTo(2));
        Assert.That(changes.ToDelete, Does.Contain(orphan));
        Assert.That(changes.ToDelete, Does.Contain(staleSentinel));
    }

    [Test]
    public void UnmatchedCategory_IsSkippedButNotOrphaned()
    {
        // Category 77 is reported by the provider but is not part of the hut's bed categories.
        var existing77 = new Availability { Hutid = HutId, Date = Day, BedCategoryId = 77, FreeRoom = 5 };

        var changes = AvailabilityReconciler.Reconcile(
            HutId, [existing77], [OpenDay(new() { ["5"] = 3, ["77"] = 1 })], HutInfo(), UpdateTime);

        // 5 added; 77 neither updated (no matching bed category) nor deleted (still reported).
        Assert.That(changes.ToAdd.Select(a => a.BedCategoryId), Is.EquivalentTo(new[] { 5 }));
        Assert.That(changes.ToUpdate, Is.Empty);
        Assert.That(changes.ToDelete, Is.Empty);
    }

    [Test]
    public void UnservicedStatus_MatchesUnservicedReservationModeOnly()
    {
        var changes = AvailabilityReconciler.Reconcile(
            HutId, [], [OpenDay(new() { ["5"] = 3 }, hutStatus: "UNSERVICED")], HutInfo(), UpdateTime);

        // categoryID 5 exists as both ROOM and UNSERVICED; UNSERVICED status must pick the UNSERVICED one.
        Assert.That(changes.ToAdd, Has.Count.EqualTo(1));
        Assert.That(changes.ToAdd[0].BedCategoryId, Is.EqualTo(5));
    }
}
