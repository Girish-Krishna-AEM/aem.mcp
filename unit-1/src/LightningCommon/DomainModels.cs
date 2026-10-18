using System.Text.Json.Serialization;

namespace LightningCommon;

public class LightningStrike
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Intensity { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class GetLightningStrikesNearLocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; }
    public string RadiusUnit { get; set; } = "km";
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }

    /// <summary>Optional pulse type filter: "CG"/"CloudToGround" or "IC"/"IntraCloud".</summary>
    public string? PulseType { get; set; }

    public bool IncludeDetails { get; set; }
}

public class GetLightningStrikesNearLocationResponse
{
    public int StrikeCount { get; set; }
    public int CloudToGroundCount { get; set; }
    public int IntraCloudCount { get; set; }
    public double MaxIntensity { get; set; }
    public List<LightningStrike> Strikes { get; set; } = new();
    public double NearestStrikeDistance { get; set; }
}

public class WeatherForecastEntry
{
    public string Date { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public int Temp { get; set; }
    public int PrecipitationPercent { get; set; }
    public int LightningRisk { get; set; }
}

public class GetWeatherForecastRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string ForecastType { get; set; } = "daily";
}

public class GetWeatherForecastResponse
{
    public List<WeatherForecastEntry> Forecast { get; set; } = new();
}

public class GetDailyWeatherForecastRequest
{
    public string? ZipCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class DailyForecastPeriod
{
    public string DateUtc { get; set; } = string.Empty;
    public bool Night { get; set; }
    public double CloudPct { get; set; }
    public double DewPointC { get; set; }
    public double Humidity { get; set; }
    public double TempC { get; set; }
    public int PrecipCode { get; set; }
    public double PrecipPct { get; set; }
    public double StormPct { get; set; }
    public int WindDir { get; set; }
    public double WindSpeedMs { get; set; }
    public double SnowMm { get; set; }
}

public class GetDailyWeatherForecastResponse
{
    public string ForecastCreatedUtc { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<DailyForecastPeriod> Periods { get; set; } = new();
}

public class GetHourlyWeatherForecastRequest
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? SearchString { get; set; }
}

public class HourlyForecastPeriod
{
    public string DateUtc { get; set; } = string.Empty;
    public double CloudPct { get; set; }
    public double DewPointC { get; set; }
    public double Humidity { get; set; }
    public double TempC { get; set; }
    public int IconCode { get; set; }
    public int PrecipCode { get; set; }
    public double PrecipPct { get; set; }
    public double PrecipRateMm { get; set; }
    public double StormPct { get; set; }
    public int WindDir { get; set; }
    public double WindSpeedMs { get; set; }
    public double AdjPrecipPct { get; set; }
    public double SolarIrr { get; set; }
    public double Pressure { get; set; }
    public double SnowRate { get; set; }
    public double WetBulbGlobeC { get; set; }
    public double WetBulbC { get; set; }
    public double WindGustMs { get; set; }
    public double HeatIndexC { get; set; }
    public double WindChillC { get; set; }
}

public class GetHourlyWeatherForecastResponse
{
    public string ForecastCreatedUtc { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<HourlyForecastPeriod> Periods { get; set; } = new();
}

public class SensorDiagnostics
{
    public double DetectionEfficiency { get; set; }
    public double GpsVisibility { get; set; }
    public int TrackedSatellites { get; set; }
    public double NoiseLevel { get; set; }
    public double Uptime { get; set; }
    public string LastCalibration { get; set; } = string.Empty;
    public double Snr { get; set; }
    public string Status { get; set; } = "healthy";
}

public class GetSensorDiagnosticsRequest
{
    public string SensorId { get; set; } = string.Empty;
}

public class GetInformerStatusRequest
{
    public string Search { get; set; } = string.Empty;
    public long? StartDateTimeUtc { get; set; }
    public long? EndDateTimeUtc { get; set; }
}

public class InformerRecord
{
    [JsonPropertyName("custId")]
    public int CustId { get; set; }

    [JsonPropertyName("informId")]
    public int InformId { get; set; }

    [JsonPropertyName("macAddress")]
    public string MacAddress { get; set; } = string.Empty;

    [JsonPropertyName("informName")]
    public string InformName { get; set; } = string.Empty;

    [JsonPropertyName("pasUserId")]
    public string PasUserId { get; set; } = string.Empty;

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("custName")]
    public string CustName { get; set; } = string.Empty;
}

public class InformerSearchMatch
{
    public int InformId { get; set; }
    public string InformName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string CustName { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
}

public class AlertRecord
{
    public string Atn { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? ActionUtc { get; set; }
    public DateTime? IssuedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? AutoClearUtc { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
}

public class AlertSiteGroup
{
    public string SiteName { get; set; } = string.Empty;
    public IReadOnlyList<AlertRecord> Alerts { get; set; } = Array.Empty<AlertRecord>();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InformerStatusResultKind
{
    Found,
    Disabled,
    Ambiguous,
    NotFound
}

public class InformerStatusResult
{
    public InformerStatusResultKind Kind { get; }
    public InformerSearchMatch? Match { get; }
    public IReadOnlyList<AlertSiteGroup> AlertSiteGroups { get; }
    public IReadOnlyList<InformerSearchMatch> Candidates { get; }

    private InformerStatusResult(
        InformerStatusResultKind kind,
        InformerSearchMatch? match,
        IReadOnlyList<AlertSiteGroup> alertSiteGroups,
        IReadOnlyList<InformerSearchMatch> candidates)
    {
        Kind = kind;
        Match = match;
        AlertSiteGroups = alertSiteGroups;
        Candidates = candidates;
    }

    public static InformerStatusResult Found(InformerSearchMatch match, IReadOnlyList<AlertSiteGroup> alertSiteGroups) =>
        new(InformerStatusResultKind.Found, match, alertSiteGroups, Array.Empty<InformerSearchMatch>());

    public static InformerStatusResult Disabled(InformerSearchMatch match) =>
        new(InformerStatusResultKind.Disabled, match, Array.Empty<AlertSiteGroup>(), Array.Empty<InformerSearchMatch>());

    public static InformerStatusResult Ambiguous(IReadOnlyList<InformerSearchMatch> candidates) =>
        new(InformerStatusResultKind.Ambiguous, null, Array.Empty<AlertSiteGroup>(), candidates);

    public static readonly InformerStatusResult NotFound =
        new(InformerStatusResultKind.NotFound, null, Array.Empty<AlertSiteGroup>(), Array.Empty<InformerSearchMatch>());
}
