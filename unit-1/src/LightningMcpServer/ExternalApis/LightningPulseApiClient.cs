using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LightningMcpServer.ExternalApis;

public class LightningPulseApiClient : ILightningPulseApiClient
{
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss";

    private readonly HttpClient _httpClient;
    private readonly LightningPulseApiOptions _options;
    private readonly ILogger<LightningPulseApiClient> _logger;

    public LightningPulseApiClient(
        HttpClient httpClient,
        IOptions<LightningPulseApiOptions> options,
        ILogger<LightningPulseApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public async Task<PulsesApiResponse> GetPulsesAsync(
        DateTime? startDateTime,
        DateTime? endDateTime,
        int? pulseType,
        CancellationToken cancellationToken = default)
    {
        var end = endDateTime ?? DateTime.UtcNow;
        var start = startDateTime ?? end.AddMinutes(-5);

        var query = $"v1/pulses?startDateTime={start.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}" +
                     $"&endDateTime={end.ToString(DateTimeFormat, CultureInfo.InvariantCulture)}";

        if (pulseType is not null)
        {
            query += $"&type={pulseType.Value}";
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, query);
        requestMessage.Headers.Add("x-api-key", _options.ApiKey);

        var fullUrl = new Uri(_httpClient.BaseAddress!, query);
        var apiKeyPresent = !string.IsNullOrEmpty(_options.ApiKey);
        var apiKeySuffix = apiKeyPresent && _options.ApiKey.Length >= 4
            ? _options.ApiKey[^4..]
            : string.Empty;

        _logger.LogInformation(
            "Calling lightning pulse API: {Method} {Url} (apiKeyPresent={ApiKeyPresent}, apiKeySuffix=****{ApiKeySuffix})",
            HttpMethod.Get,
            fullUrl,
            apiKeyPresent,
            apiKeySuffix);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to reach lightning pulse API at {Url} after {ElapsedMs}ms", fullUrl, stopwatch.ElapsedMilliseconds);
            throw new LightningPulseApiException("Failed to reach the lightning pulse API.", ex);
        }
        stopwatch.Stop();

        _logger.LogInformation(
            "Lightning pulse API responded: {StatusCode} for {Url} in {ElapsedMs}ms",
            (int)response.StatusCode,
            fullUrl,
            stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Lightning pulse API error response: {StatusCode} for {Url}. Body: {Body}",
                (int)response.StatusCode,
                fullUrl,
                body);
            throw new LightningPulseApiException(
                $"Lightning pulse API returned status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        PulsesApiResponse? parsed;
        try
        {
            parsed = await response.Content.ReadFromJsonAsync<PulsesApiResponse>(cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new LightningPulseApiException("Lightning pulse API returned a malformed response.", ex);
        }

        return parsed ?? new PulsesApiResponse();
    }
}

public class LightningPulseApiException : Exception
{
    public LightningPulseApiException(string message) : base(message) { }
    public LightningPulseApiException(string message, Exception innerException) : base(message, innerException) { }
}
