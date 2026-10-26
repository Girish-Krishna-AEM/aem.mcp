using System;

namespace LightningCommon;

public static class GeoDistanceUtils
{
    public const double EarthRadiusKm = 6371.0;
    public const double KmPerMile = 1.60934;

    public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static double CalculateDistanceMiles(double lat1, double lon1, double lat2, double lon2) =>
        CalculateDistanceKm(lat1, lon1, lat2, lon2) / KmPerMile;

    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
