using Xunit;
using LightningCommon;

namespace LightningMcpServer.Tests;

public class GeoDistanceUtilsTests
{
    [Fact]
    public void CalculateDistanceKm_SamePoint_ReturnsZero()
    {
        var distance = GeoDistanceUtils.CalculateDistanceKm(30.3085, -82.2741, 30.3085, -82.2741);

        Assert.Equal(0, distance, 6);
    }

    [Fact]
    public void CalculateDistanceKm_OneDegreeLatitudeApart_IsApproximately111Km()
    {
        var distance = GeoDistanceUtils.CalculateDistanceKm(0, 0, 1, 0);

        Assert.Equal(111.19, distance, 1);
    }

    [Fact]
    public void CalculateDistanceMiles_MatchesKmDividedByKmPerMile()
    {
        var km = GeoDistanceUtils.CalculateDistanceKm(0, 0, 0.2, 0);
        var miles = GeoDistanceUtils.CalculateDistanceMiles(0, 0, 0.2, 0);

        Assert.Equal(km / GeoDistanceUtils.KmPerMile, miles, 6);
    }
}
