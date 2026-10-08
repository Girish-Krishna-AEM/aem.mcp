using System.Text.Json.Serialization;

namespace LightningMcpServer.ExternalApis;

public interface IForecastApiEnvelope
{
    int Code { get; }
    string? ErrorMessage { get; }
}

public class DailyForecastApiResponse : IForecastApiEnvelope
{
    [JsonPropertyName("Result")]
    public DailyForecastApiResult? Result { get; set; }

    [JsonPropertyName("Code")]
    public int Code { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}

public class DailyForecastApiResult
{
    [JsonPropertyName("Latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("Longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("DailyForecastPeriods")]
    public List<DailyForecastPeriodDto> DailyForecastPeriods { get; set; } = new();
}

public class DailyForecastPeriodDto
{
    [JsonPropertyName("ForecastDateUtcStr")]
    public string? ForecastDateUtcStr { get; set; }

    [JsonPropertyName("IsNightTimePeriod")]
    public bool IsNightTimePeriod { get; set; }

    [JsonPropertyName("CloudCoverPercent")]
    public double CloudCoverPercent { get; set; }

    [JsonPropertyName("DewPointC")]
    public double DewPointC { get; set; }

    [JsonPropertyName("RelativeHumidity")]
    public double RelativeHumidity { get; set; }

    [JsonPropertyName("TemperatureC")]
    public double TemperatureC { get; set; }

    [JsonPropertyName("PrecipCode")]
    public int PrecipCode { get; set; }

    [JsonPropertyName("PrecipProbability")]
    public double PrecipProbability { get; set; }

    [JsonPropertyName("ThunderstormProbability")]
    public double ThunderstormProbability { get; set; }

    [JsonPropertyName("WindDirectionDegrees")]
    public int WindDirectionDegrees { get; set; }

    [JsonPropertyName("WindSpeedMetersPerSecond")]
    public double WindSpeedMetersPerSecond { get; set; }

    [JsonPropertyName("SnowAmountMm")]
    public double SnowAmountMm { get; set; }
}

public class HourlyForecastApiResponse : IForecastApiEnvelope
{
    [JsonPropertyName("Result")]
    public HourlyForecastApiResult? Result { get; set; }

    [JsonPropertyName("Code")]
    public int Code { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}

public class HourlyForecastApiResult
{
    [JsonPropertyName("Latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("Longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("HourlyForecastPeriods")]
    public List<HourlyForecastPeriodDto> HourlyForecastPeriods { get; set; } = new();
}

public class HourlyForecastPeriodDto
{
    [JsonPropertyName("ForecastDateUtcStr")]
    public string? ForecastDateUtcStr { get; set; }

    [JsonPropertyName("CloudCoverPercent")]
    public double CloudCoverPercent { get; set; }

    [JsonPropertyName("DewPointC")]
    public double DewPointC { get; set; }

    [JsonPropertyName("RelativeHumidity")]
    public double RelativeHumidity { get; set; }

    [JsonPropertyName("TemperatureC")]
    public double TemperatureC { get; set; }

    [JsonPropertyName("IconCode")]
    public int IconCode { get; set; }

    [JsonPropertyName("PrecipCode")]
    public int PrecipCode { get; set; }

    [JsonPropertyName("PrecipProbability")]
    public double PrecipProbability { get; set; }

    [JsonPropertyName("PrecipRateMillimeters")]
    public double PrecipRateMillimeters { get; set; }

    [JsonPropertyName("ThunderstormProbability")]
    public double ThunderstormProbability { get; set; }

    [JsonPropertyName("WindDirectionDegrees")]
    public int WindDirectionDegrees { get; set; }

    [JsonPropertyName("WindSpeedMetersPerSecond")]
    public double WindSpeedMetersPerSecond { get; set; }

    [JsonPropertyName("AdjustedPrecipProbability")]
    public double AdjustedPrecipProbability { get; set; }

    [JsonPropertyName("SolarIrradiance")]
    public double SolarIrradiance { get; set; }

    [JsonPropertyName("SurfacePressure")]
    public double SurfacePressure { get; set; }

    [JsonPropertyName("SnowRate")]
    public double SnowRate { get; set; }

    [JsonPropertyName("WetBulbGlobeTemperatureC")]
    public double WetBulbGlobeTemperatureC { get; set; }

    [JsonPropertyName("WetBulbTemperatureC")]
    public double WetBulbTemperatureC { get; set; }

    [JsonPropertyName("WindGustMps")]
    public double WindGustMps { get; set; }

    [JsonPropertyName("HeatIndexC")]
    public double HeatIndexC { get; set; }

    [JsonPropertyName("WindChillC")]
    public double WindChillC { get; set; }
}
