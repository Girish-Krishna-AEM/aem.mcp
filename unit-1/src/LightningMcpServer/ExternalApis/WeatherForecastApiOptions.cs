namespace LightningMcpServer.ExternalApis;

public class WeatherForecastApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string DailyByZipCodePath { get; set; } = "Forecasts/v2/DailyForecast/ByZipCode";
    public string DailyByLatLonPath { get; set; } = "Forecasts/v2/DailyForecast/ByLatitudeAndLongitude";
    public string HourlyByLatLonPath { get; set; } = "Forecasts/v1/HourlyForecast/ByLatitudeAndLongitude";
    public string HourlyBySearchPath { get; set; } = "Forecasts/v1/HourlyForecast/BySearch";
}
