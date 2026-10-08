using LightningCommon;
using LightningMcpServer.ExternalApis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LightningMcpServer.DomainModules;

public class StrikeDetectionModule : IStrikeDetectionModule
{
    private const int MaxStrikesReturned = 25;

    private readonly ILogger<StrikeDetectionModule> _logger;
    private readonly ILightningPulseApiClient _pulseApiClient;

    public StrikeDetectionModule(ILogger<StrikeDetectionModule> logger, ILightningPulseApiClient pulseApiClient)
    {
        _logger = logger;
        _pulseApiClient = pulseApiClient;
    }

    public GetLightningStrikesNearLocationResponse GetLightningStrikesNearLocation(
        GetLightningStrikesNearLocationRequest request)
    {
        _logger.LogInformation(
            "Strike detection requested: location=({Latitude},{Longitude}), radius={Radius} {RadiusUnit}, " +
            "startDateTime={StartDateTime}, endDateTime={EndDateTime}, pulseType={PulseType}, includeDetails={IncludeDetails}",
            request.Latitude, request.Longitude, request.Radius, request.RadiusUnit,
            request.StartDateTime, request.EndDateTime, request.PulseType, request.IncludeDetails);

        ValidateRequest(request);

        var pulseType = MapPulseType(request.PulseType);

        _logger.LogInformation(
            "Calling pulse API with startDateTime={StartDateTime}, endDateTime={EndDateTime}, mappedPulseType={MappedPulseType}",
            request.StartDateTime, request.EndDateTime, pulseType);

        var apiResponse = _pulseApiClient
            .GetPulsesAsync(
                request.StartDateTime, request.EndDateTime, pulseType,
                request.Latitude, request.Longitude, request.Radius, request.RadiusUnit)
            .GetAwaiter()
            .GetResult();

        var radiusKm = request.RadiusUnit == "miles" ? request.Radius * 1.60934 : request.Radius;

        var strikes = new List<LightningStrike>();
        double nearestDistance = double.MaxValue;

        foreach (var pulse in apiResponse.Pulses ?? new List<PulseDto>())
        {
            var distance = CalculateDistance(request.Latitude, request.Longitude, pulse.Lat, pulse.Lon);
            if (distance > radiusKm)
            {
                continue;
            }

            strikes.Add(new LightningStrike
            {
                Latitude = pulse.Lat,
                Longitude = pulse.Lon,
                Intensity = pulse.Cur,
                Type = pulse.Typ == 0 ? "cg" : "ic",
                Timestamp = pulse.Ts
            });

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        if (strikes.Count == 0)
        {
            nearestDistance = 0;
        }

        var cgCount = strikes.Count(s => s.Type == "cg");
        var icCount = strikes.Count(s => s.Type == "ic");
        var maxIntensity = strikes.Count > 0 ? strikes.Max(s => s.Intensity) : 0;

        _logger.LogInformation(
            "Strike detection result: count={StrikeCount}, cg={CgCount}, ic={IcCount}, maxIntensity={MaxIntensity}, nearest={NearestDistance:F2}km",
            strikes.Count, cgCount, icCount, maxIntensity, nearestDistance);

        return new GetLightningStrikesNearLocationResponse
        {
            StrikeCount = strikes.Count,
            CloudToGroundCount = cgCount,
            IntraCloudCount = icCount,
            MaxIntensity = maxIntensity,
            Strikes = request.IncludeDetails
                ? strikes.OrderByDescending(s => s.Intensity).Take(MaxStrikesReturned).ToList()
                : new List<LightningStrike>(),
            NearestStrikeDistance = nearestDistance
        };
    }

    private static int? MapPulseType(string? pulseType)
    {
        if (string.IsNullOrWhiteSpace(pulseType))
        {
            return null;
        }

        return pulseType.ToUpperInvariant() switch
        {
            "CG" or "CLOUDTOGROUND" => 0,
            "IC" or "INTRACLOUD" => 1,
            _ => throw new ArgumentException($"Unsupported pulse type '{pulseType}'. Use 'CG'/'CloudToGround' or 'IC'/'IntraCloud'.")
        };
    }

    private void ValidateRequest(GetLightningStrikesNearLocationRequest request)
    {
        if (double.IsNaN(request.Latitude) || double.IsNaN(request.Longitude))
            throw new ArgumentException("Invalid location coordinates");

        if (request.Radius <= 0)
            throw new ArgumentException("Radius must be greater than 0");

        if (!new[] { "km", "miles" }.Contains(request.RadiusUnit))
            throw new ArgumentException("RadiusUnit must be 'km' or 'miles'");
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
