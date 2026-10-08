namespace LightningMcpServer.ExternalApis;

public interface IWeatherForecastApiClient
{
    Task<DailyForecastApiResponse> GetDailyForecastByZipCodeAsync(string zipCode, CancellationToken cancellationToken = default);

    Task<DailyForecastApiResponse> GetDailyForecastByLatLonAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    Task<HourlyForecastApiResponse> GetHourlyForecastByLatLonAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    Task<HourlyForecastApiResponse> GetHourlyForecastBySearchAsync(string searchString, CancellationToken cancellationToken = default);
}
