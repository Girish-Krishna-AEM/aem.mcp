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

public class InformerStatus
{
    public string Status { get; set; } = "active";
    public string LastActivation { get; set; } = string.Empty;
    public string PowerStatus { get; set; } = "normal";
    public string Zone { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
}

public class GetInformerStatusRequest
{
    public string? InformerId { get; set; }
    public string? Zone { get; set; }
}
