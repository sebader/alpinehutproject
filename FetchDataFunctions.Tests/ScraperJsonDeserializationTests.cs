using System.Text.Json;
using FetchDataFunctions.Models;

namespace FetchDataFunctions.Tests;

[TestFixture]
public class ScraperJsonDeserializationTests
{
    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", name));

    [Test]
    public void HutInfo_RealPayload_DeserializesThroughSourceGenOptions()
    {
        var info = JsonSerializer.Deserialize<HutInfoV2>(ReadFixture("hutinfo29.json"), ScraperJson.Options);

        Assert.That(info, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(info!.hutId, Is.EqualTo(29));
            Assert.That(info.hutName, Is.EqualTo("Tällihütte"));
            Assert.That(info.tenantCountry, Is.EqualTo("CH"));
            Assert.That(info.coordinates, Is.EqualTo("668.000 / 176.940"));
            Assert.That(info.hutBedCategories, Has.Length.EqualTo(2));
        });

        var cat = info!.hutBedCategories.Single(c => c.categoryID == 5);
        Assert.That(cat.tenantBedCategoryId, Is.EqualTo(1));
        Assert.That(cat.reservationMode, Is.EqualTo("ROOM"));
        Assert.That(cat.hutBedCategoryLanguageData.Any(l => l.language == "DE_CH" && l.label == "Massenlager"), Is.True);
    }

    [Test]
    public void Availability_RealPayload_DeserializesThroughSourceGenOptions()
    {
        var days = JsonSerializer.Deserialize<IEnumerable<AvailabilityData>>(ReadFixture("avail29.json"), ScraperJson.Options)?.ToList();

        Assert.That(days, Is.Not.Null.And.Not.Empty);
        var first = days![0];
        Assert.Multiple(() =>
        {
            Assert.That(first.hutStatus, Is.EqualTo("SERVICED"));
            Assert.That(first.freeBedsPerCategory["5"], Is.EqualTo(1));
            Assert.That(first.freeBedsPerCategory["36"], Is.EqualTo(1));
        });
    }
}
