using FetchDataFunctions;

namespace FetchDataFunctions.Tests;

[TestFixture]
public class HelpersCoordinateTests
{
    [Test]
    public void CoordinatesSanityCheck_CentralEurope_IsTrue()
    {
        Assert.That(Helpers.CoordinatesSanityCheck(longitude: 8.2, latitude: 46.7), Is.True);
    }

    [Test]
    public void CoordinatesSanityCheck_OutsideEurope_IsFalse()
    {
        Assert.That(Helpers.CoordinatesSanityCheck(longitude: 0, latitude: 0), Is.False);
        // Raw Swiss-grid values are far outside the WGS84 Europe box.
        Assert.That(Helpers.CoordinatesSanityCheck(longitude: 587.168, latitude: 234.694), Is.False);
    }

    [Test]
    public void IsValidWgs84Coordinate_InRange_IsTrue()
    {
        Assert.That(Helpers.IsValidWgs84Coordinate(latitude: 46.7, longitude: 8.2), Is.True);
    }

    [Test]
    public void IsValidWgs84Coordinate_ProjectedGrid_IsFalse()
    {
        Assert.That(Helpers.IsValidWgs84Coordinate(latitude: 587.168, longitude: 234.694), Is.False);
    }

    [Test]
    public void Ch1903ToWgs84_BernOrigin_MatchesKnownReference()
    {
        // swisstopo reference: LV03 600000/200000 is the projection origin in Bern (46.95108, 7.43863).
        var (lat, lon) = Helpers.Ch1903ToWgs84(600000, 200000);
        Assert.That(lat, Is.EqualTo(46.95108).Within(0.001));
        Assert.That(lon, Is.EqualTo(7.43863).Within(0.001));
    }

    [Test]
    public void TryConvertSwissGridToWgs84_TaellihuetteKilometres_ConvertsToCentralEurope()
    {
        // Hut 29 (Tällihütte) reports "668.000 / 176.940" (LV03 kilometres).
        var ok = Helpers.TryConvertSwissGridToWgs84(668.000, 176.940, out var lat, out var lon);
        Assert.That(ok, Is.True);
        Assert.That(Helpers.CoordinatesSanityCheck(lon, lat), Is.True);
        Assert.That(lat, Is.EqualTo(46.72).Within(0.2));
        Assert.That(lon, Is.EqualTo(8.19).Within(0.2));
    }

    [Test]
    public void TryConvertSwissGridToWgs84_OrderIndependent()
    {
        Helpers.TryConvertSwissGridToWgs84(668.000, 176.940, out var lat1, out var lon1);
        var ok = Helpers.TryConvertSwissGridToWgs84(176.940, 668.000, out var lat2, out var lon2);
        Assert.That(ok, Is.True);
        Assert.That(lat2, Is.EqualTo(lat1).Within(1e-9));
        Assert.That(lon2, Is.EqualTo(lon1).Within(1e-9));
    }

    [Test]
    public void TryConvertSwissGridToWgs84_Wgs84Input_IsRejected()
    {
        Assert.That(Helpers.TryConvertSwissGridToWgs84(46.7, 8.2, out _, out _), Is.False);
    }

    [Test]
    public void TryConvertSwissGridToWgs84_Metres_AlsoConverts()
    {
        var ok = Helpers.TryConvertSwissGridToWgs84(668000, 176940, out var lat, out var lon);
        Assert.That(ok, Is.True);
        Assert.That(Helpers.CoordinatesSanityCheck(lon, lat), Is.True);
    }

    [Test]
    public void DistanceInKm_SamePoint_IsZero()
    {
        Assert.That(Helpers.DistanceInKm(46.96, 11.18, 46.96, 11.18), Is.EqualTo(0).Within(1e-9));
    }

    [Test]
    public void DistanceInKm_MuellerhuetteRoundingOffset_IsAboutFourHundredMetres()
    {
        // Hut 570 stored "46.96/11.18" vs the OSM alpine_hut node at 46.9631512/11.1778146 (~0.39 km).
        var distance = Helpers.DistanceInKm(46.96, 11.18, 46.9631512, 11.1778146);
        Assert.That(distance, Is.EqualTo(0.39).Within(0.05));
    }

    [Test]
    public void DistanceInKm_DifferentHutsFarApart_ExceedsRefinementGuard()
    {
        // A same-named hut in a different valley must fall well outside the 2 km refinement guard.
        var distance = Helpers.DistanceInKm(46.4992, 8.9956, 46.473108, 9.0236542);
        Assert.That(distance, Is.GreaterThan(2.0));
    }
}
