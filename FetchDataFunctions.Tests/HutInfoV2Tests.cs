using FetchDataFunctions.Models;

namespace FetchDataFunctions.Tests;

[TestFixture]
public class HutInfoV2Tests
{
    [Test]
    [SetCulture("en-US")]
    public void Coordinates_SlashSeparated_AreParsed()
    {
        var info = new HutInfoV2 { coordinates = "668.000 / 176.940" };
        Assert.That(info.Latitude, Is.EqualTo(668.0).Within(1e-6));
        Assert.That(info.Longitude, Is.EqualTo(176.94).Within(1e-6));
    }

    [Test]
    [SetCulture("en-US")]
    public void Coordinates_CommaSeparated_AreParsed()
    {
        var info = new HutInfoV2 { coordinates = "46.7, 8.2" };
        Assert.That(info.Latitude, Is.EqualTo(46.7).Within(1e-6));
        Assert.That(info.Longitude, Is.EqualTo(8.2).Within(1e-6));
    }

    [Test]
    public void Coordinates_Null_ReturnsNull()
    {
        var info = new HutInfoV2 { coordinates = null };
        Assert.That(info.Latitude, Is.Null);
        Assert.That(info.Longitude, Is.Null);
    }

    [TestCase("1726 m", 1726)]
    [TestCase("1.726 m", 1726)]
    [TestCase("2500 M", 2500)]
    [TestCase("3000 m ü M", 3000)]
    [TestCase("not-a-number", null)]
    public void AltitudeInt_IsNormalized(string altitude, int? expected)
    {
        var info = new HutInfoV2 { altitude = altitude };
        Assert.That(info.AltitudeInt, Is.EqualTo(expected));
    }

    [TestCase("http://www.taelli.com", "http://www.taelli.com")]
    [TestCase("www.example.com", "http://www.example.com")]
    [TestCase("HTTPS://EXAMPLE.COM", "https://example.com")]
    public void HutWebsiteNormalized_AddsSchemeAndLowercases(string input, string expected)
    {
        var info = new HutInfoV2 { hutWebsite = input };
        Assert.That(info.HutWebsiteNormalized, Is.EqualTo(expected));
    }

    [Test]
    public void HutWebsiteNormalized_Empty_ReturnsNull()
    {
        Assert.That(new HutInfoV2 { hutWebsite = "" }.HutWebsiteNormalized, Is.Null);
    }

    [TestCase("AT", "Österreich")]
    [TestCase("CH", "Schweiz")]
    [TestCase("DE", "Deutschland")]
    [TestCase("IT", "Italien")]
    [TestCase("SI", "SI")]
    public void CountryNormalized_MapsKnownCodes(string code, string expected)
    {
        var info = new HutInfoV2 { tenantCountry = code };
        Assert.That(info.CountryNormalized, Is.EqualTo(expected));
    }
}
