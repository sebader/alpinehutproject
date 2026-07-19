using FetchDataFunctions.Models;

namespace FetchDataFunctions.Tests;

[TestFixture]
public class AvailabilityDataTests
{
    [Test]
    public void DateNormalized_StripsTime()
    {
        var data = new AvailabilityData { date = new DateTime(2026, 7, 19, 13, 45, 0, DateTimeKind.Utc) };
        Assert.That(data.DateNormalized, Is.EqualTo(new DateTime(2026, 7, 19)));
    }

    [TestCase("SERVICED", "AVAILABLE", false)]
    [TestCase("CLOSED", "AVAILABLE", true)]
    [TestCase("closed", "AVAILABLE", true)]
    [TestCase("SERVICED", "CLOSED", true)]
    public void HutClosed_ReflectsStatusOrPercentage(string hutStatus, string percentage, bool expected)
    {
        var data = new AvailabilityData { hutStatus = hutStatus, percentage = percentage };
        Assert.That(data.HutClosed, Is.EqualTo(expected));
    }
}
