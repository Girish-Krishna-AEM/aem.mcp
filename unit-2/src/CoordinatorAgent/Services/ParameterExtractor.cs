using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public class ParameterExtractor : IParameterExtractor
{
    private const double MaxRadiusMiles = 100;
    private const double MilesPerKilometer = 0.621371;

    private static readonly Regex CoordinatePattern = new(
        @"lat(?:itude)?\s*[:=]?\s*(-?\d+(?:\.\d+)?)\s*,?\s*long?(?:itude)?\s*[:=]?\s*(-?\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RadiusPattern = new(
        @"(\d+(?:\.\d+)?)\s*(kilometers?|km|miles?|mi)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TimeWindowPattern = new(
        @"\b(?:last|past)\s+(\d+)\s*(minutes?|mins?|hours?|hrs?|days?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex YesterdayPattern = new(
        @"\byesterday\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExplicitDateRangePattern = new(
        @"\b(?:from|between)\s+(\d{4}-\d{2}-\d{2}(?:[T\s]\d{2}:\d{2}(?::\d{2})?)?)\s+(?:to|and)\s+(\d{4}-\d{2}-\d{2}(?:[T\s]\d{2}:\d{2}(?::\d{2})?)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CloudToGroundPattern = new(
        @"\b(cloud[\s-]?to[\s-]?ground|cg)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IntraCloudPattern = new(
        @"\b(intra[\s-]?cloud|ic)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForecastTypePattern = new(
        @"\b(15[\s-]?day|daily|hourly)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SensorIdPattern = new(
        @"\b(sensor-[a-z0-9]+|[a-z]{2}-\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InformerIdPattern = new(
        @"\b(informer-[a-z0-9]+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ZonePattern = new(
        @"\bzone[\s-]?[a-z0-9]+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches the free-text location phrase so it can be sent to the geocoder.
    // Captures "near/in/at/for <location>" up to a trailing qualifier (within/over/in the last, etc.) or end of string.
    private static readonly Regex LocationPhrasePattern = new(
        @"\b(?:near|in|at|for)\s+([A-Za-z0-9][A-Za-z0-9.,'\-\s]*?)(?=\s*(?:within|over|around|in\s+the\s+last|in\s+the\s+past|yesterday\b|(?:from|between)\s+\d{4}-\d{2}-\d{2}|\d+(?:\.\d+)?\s*(?:kilometers?|km|miles?|mi)\b|\?|$))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<ParameterExtractor> _logger;
    private readonly ILocationGeocoder _locationGeocoder;
    private readonly TimeZoneInfo _userTimeZone;

    public ParameterExtractor(ILogger<ParameterExtractor> logger, ILocationGeocoder locationGeocoder, TimeZoneInfo userTimeZone)
    {
        _logger = logger;
        _locationGeocoder = locationGeocoder;
        _userTimeZone = userTimeZone;
    }

    public async Task<(bool Success, JsonElement? Parameters, string? Error)> ExtractParameters(string query, Intent intent)
    {
        return intent switch
        {
            Intent.Strike => await ExtractStrikeParameters(query),
            Intent.Weather => await ExtractWeatherParameters(query),
            Intent.Sensor => ExtractSensorParameters(query),
            Intent.Informer => ExtractInformerParameters(query),
            _ => (false, null, "Unrecognized intent.")
        };
    }

    private async Task<(bool, JsonElement?, string?)> ExtractStrikeParameters(string query)
    {
        var (locationFound, latitude, longitude, locationError) = await TryExtractLocation(query);
        if (!locationFound)
        {
            return (false, null, locationError);
        }

        var (radius, radiusUnit) = TryExtractRadius(query);

        var radiusMiles = radiusUnit == "miles" ? radius : radius * MilesPerKilometer;
        if (radiusMiles > MaxRadiusMiles)
        {
            _logger.LogWarning("Radius {Radius} {RadiusUnit} exceeds the maximum allowed radius", radius, radiusUnit);
            return (false, null, $"Radius of {radius} {radiusUnit} exceeds the maximum allowed radius of {MaxRadiusMiles} miles.");
        }

        var (timeWindowSuccess, startDateTime, endDateTime, timeWindowError) = TryExtractTimeWindow(query);
        if (!timeWindowSuccess)
        {
            return (false, null, timeWindowError);
        }

        var pulseType = TryExtractPulseType(query);

        var parameters = new
        {
            latitude,
            longitude,
            radius,
            radiusUnit,
            startDateTime,
            endDateTime,
            pulseType
        };

        return (true, SerializeParameters(parameters), null);
    }

    private async Task<(bool, JsonElement?, string?)> ExtractWeatherParameters(string query)
    {
        var (locationFound, latitude, longitude, locationError) = await TryExtractLocation(query);
        if (!locationFound)
        {
            return (false, null, locationError);
        }

        var forecastMatch = ForecastTypePattern.Match(query);
        var normalizedMatch = forecastMatch.Success
            ? forecastMatch.Value.Replace(" ", "").Replace("-", "").ToLowerInvariant()
            : "daily";
        var forecastType = normalizedMatch switch
        {
            "15day" => "15-day",
            "hourly" => "hourly",
            _ => "daily"
        };

        var parameters = new
        {
            latitude,
            longitude,
            forecastType
        };

        return (true, SerializeParameters(parameters), null);
    }

    private (bool, JsonElement?, string?) ExtractSensorParameters(string query)
    {
        var match = SensorIdPattern.Match(query);
        if (!match.Success)
        {
            _logger.LogWarning("Missing parameter: sensorId");
            return (false, null, "Missing required parameter: sensor_id. Please specify a sensor identifier (e.g., 'sensor-001' or 'TX-123').");
        }

        var parameters = new { sensorId = match.Value };
        return (true, SerializeParameters(parameters), null);
    }

    private (bool, JsonElement?, string?) ExtractInformerParameters(string query)
    {
        var informerMatch = InformerIdPattern.Match(query);
        var zoneMatch = ZonePattern.Match(query);

        if (!informerMatch.Success && !zoneMatch.Success)
        {
            _logger.LogWarning("Missing parameter: informerId or zone");
            return (false, null, "Missing required parameter: informer_id or zone. Please specify a device (e.g., 'informer-001') or a zone (e.g., 'zone-a').");
        }

        var parameters = new
        {
            informerId = informerMatch.Success ? informerMatch.Value : null,
            zone = zoneMatch.Success ? zoneMatch.Value : null
        };

        return (true, SerializeParameters(parameters), null);
    }

    private async Task<(bool Found, double Latitude, double Longitude, string? Error)> TryExtractLocation(string query)
    {
        var coordMatch = CoordinatePattern.Match(query);
        if (coordMatch.Success)
        {
            var lat = double.Parse(coordMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var lon = double.Parse(coordMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            return (true, lat, lon, null);
        }

        var locationText = ExtractLocationPhrase(query);
        if (locationText is null)
        {
            _logger.LogWarning("Missing parameter: location");
            return (false, 0, 0, LocationError);
        }

        var outcome = await _locationGeocoder.GeocodeAsync(locationText);

        switch (outcome.Kind)
        {
            case GeocodeOutcomeKind.Resolved:
                return (true, outcome.Result!.Latitude, outcome.Result.Longitude, null);

            case GeocodeOutcomeKind.Ambiguous:
                var candidateList = string.Join("; ", outcome.Candidates);
                return (false, 0, 0,
                    $"Multiple locations match '{locationText}'. Did you mean: {candidateList}? Please specify state or ZIP.");

            case GeocodeOutcomeKind.NotFound:
            default:
                return (false, 0, 0,
                    $"Could not resolve location '{locationText}'. Please provide a valid US city/state, ZIP code, or street address.");
        }
    }

    private static string? ExtractLocationPhrase(string query)
    {
        var match = LocationPhrasePattern.Match(query);
        if (!match.Success)
        {
            return null;
        }

        var phrase = match.Groups[1].Value.Trim().TrimEnd('?', '.', ',');
        return string.IsNullOrWhiteSpace(phrase) ? null : phrase;
    }

    private static (double Radius, string RadiusUnit) TryExtractRadius(string query)
    {
        var match = RadiusPattern.Match(query);
        if (!match.Success)
        {
            return (50, "km");
        }

        var value = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value.StartsWith("mi", StringComparison.OrdinalIgnoreCase) ? "miles" : "km";
        return (value, unit);
    }

    private (bool Success, DateTime? StartDateTime, DateTime? EndDateTime, string? Error) TryExtractTimeWindow(string query)
    {
        var explicitMatch = ExplicitDateRangePattern.Match(query);
        if (explicitMatch.Success)
        {
            return TryParseExplicitDateRange(explicitMatch.Groups[1].Value, explicitMatch.Groups[2].Value);
        }

        if (YesterdayPattern.IsMatch(query))
        {
            // "Yesterday" means the user's local calendar day, not UTC's — convert its
            // local midnight-to-midnight boundaries to UTC for the downstream API.
            var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _userTimeZone).Date;
            var yesterdayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddDays(-1), _userTimeZone);
            var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, _userTimeZone);
            return (true, yesterdayStartUtc, todayStartUtc, null);
        }

        var match = TimeWindowPattern.Match(query);
        if (!match.Success)
        {
            return (true, null, null, null);
        }

        var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value.ToLowerInvariant();

        var end = DateTime.UtcNow;
        var start = unit.StartsWith("min", StringComparison.OrdinalIgnoreCase)
            ? end.AddMinutes(-amount)
            : unit.StartsWith("hour", StringComparison.OrdinalIgnoreCase) || unit.StartsWith("hr", StringComparison.OrdinalIgnoreCase)
                ? end.AddHours(-amount)
                : end.AddDays(-amount);

        return (true, start, end, null);
    }

    private (bool Success, DateTime? StartDateTime, DateTime? EndDateTime, string? Error) TryParseExplicitDateRange(
        string startText, string endText)
    {
        // The user's query never carries a timezone offset — the date/time they typed is
        // always their own local wall-clock time, which must be converted to UTC before
        // it's sent downstream (the Lightning Pulse API expects startDateTime/endDateTime in UTC).
        if (!DateTime.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startLocal) ||
            !DateTime.TryParse(endText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endLocal))
        {
            return (false, null, null,
                $"Could not parse date range '{startText}' to '{endText}'. Use an ISO format like '2026-01-01' or '2026-01-01T10:00:00'.");
        }

        var start = TimeZoneInfo.ConvertTimeToUtc(startLocal, _userTimeZone);
        var end = TimeZoneInfo.ConvertTimeToUtc(endLocal, _userTimeZone);

        if (start >= end)
        {
            return (false, null, null, "Invalid date range: start date/time must be before end date/time.");
        }

        return (true, start, end, null);
    }

    private static string? TryExtractPulseType(string query)
    {
        if (CloudToGroundPattern.IsMatch(query))
        {
            return "CG";
        }

        if (IntraCloudPattern.IsMatch(query))
        {
            return "IC";
        }

        return null;
    }

    private static JsonElement SerializeParameters(object parameters)
    {
        var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        return JsonDocument.Parse(json).RootElement;
    }

    private const string LocationError =
        "Missing required parameter: location. Please provide a location (e.g., 'Austin, TX', 'Germantown MD', '20874', or 'latitude 30.27, longitude -97.74').";
}
