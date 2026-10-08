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
