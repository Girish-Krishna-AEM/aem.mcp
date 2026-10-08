using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LightningMcpServer.ExternalApis;

public class WeatherForecastApiClient : IWeatherForecastApiClient
{
    private readonly HttpClient _httpClient;
    private readonly WeatherForecastApiOptions _options;
    private readonly ILogger<WeatherForecastApiClient> _logger;

    public WeatherForecastApiClient(
        HttpClient httpClient,
        IOptions<WeatherForecastApiOptions> options,
        ILogger<WeatherForecastApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public Task<DailyForecastApiResponse> GetDailyForecastByZipCodeAsync(string zipCode, CancellationToken cancellationToken = default) =>
        GetAsync<DailyForecastApiResponse>(
            $"{_options.DailyByZipCodePath}?ZipCode={Uri.EscapeDataString(zipCode)}", cancellationToken);

    public Task<DailyForecastApiResponse> GetDailyForecastByLatLonAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
        GetAsync<DailyForecastApiResponse>(
            $"{_options.DailyByLatLonPath}?latitude={ToInvariant(latitude)}&longitude={ToInvariant(longitude)}",
            cancellationToken);

    public Task<HourlyForecastApiResponse> GetHourlyForecastByLatLonAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
        GetAsync<HourlyForecastApiResponse>(
            $"{_options.HourlyByLatLonPath}?latitude={ToInvariant(latitude)}&Longitude={ToInvariant(longitude)}",
            cancellationToken);

    public Task<HourlyForecastApiResponse> GetHourlyForecastBySearchAsync(string searchString, CancellationToken cancellationToken = default) =>
        GetAsync<HourlyForecastApiResponse>(
            $"{_options.HourlyBySearchPath}?searchString={Uri.EscapeDataString(searchString)}", cancellationToken);

    private static string ToInvariant(double value) => value.ToString(CultureInfo.InvariantCulture);

    private async Task<T> GetAsync<T>(string query, CancellationToken cancellationToken) where T : class, IForecastApiEnvelope, new()
    {
        if (_httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException(
                "WEATHER_FORECAST_API_BASE_URL is not configured. Set it in your .env file.");
        }

        var fullUrl = new Uri(_httpClient.BaseAddress, query);
        _logger.LogInformation("Calling weather forecast API: {Method} {Url}", HttpMethod.Get, fullUrl);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(query, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to reach weather forecast API at {Url} after {ElapsedMs}ms", fullUrl, stopwatch.ElapsedMilliseconds);
            throw new WeatherForecastApiException("Failed to reach the weather forecast API.", ex);
        }
        stopwatch.Stop();

        _logger.LogInformation(
            "Weather forecast API responded: {StatusCode} for {Url} in {ElapsedMs}ms",
            (int)response.StatusCode, fullUrl, stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Weather forecast API error response: {StatusCode} for {Url}. Body: {Body}",
                (int)response.StatusCode, fullUrl, body);
            throw new WeatherForecastApiException(
                $"Weather forecast API returned status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        T? parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new WeatherForecastApiException("Weather forecast API returned a malformed response.", ex);
        }

        parsed ??= new T();

        if (parsed.Code != 0 && parsed.Code != 200)
        {
            _logger.LogError(
                "Weather forecast API returned error code {Code} for {Url}: {ErrorMessage}",
                parsed.Code, fullUrl, parsed.ErrorMessage);
            throw new WeatherForecastApiException(
                $"Weather forecast API returned error code {parsed.Code}: {parsed.ErrorMessage}");
        }

        return parsed;
    }
}

public class WeatherForecastApiException : Exception
{
    public WeatherForecastApiException(string message) : base(message) { }
    public WeatherForecastApiException(string message, Exception innerException) : base(message, innerException) { }
}
