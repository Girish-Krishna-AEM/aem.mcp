using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LightningCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LightningMcpServer.ExternalApis;

public class AlertsApiClient : IAlertsApiClient
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _httpClient;
    private readonly AlertsApiOptions _options;
    private readonly ILogger<AlertsApiClient> _logger;

    public AlertsApiClient(
        HttpClient httpClient,
        IOptions<AlertsApiOptions> options,
        ILogger<AlertsApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<IReadOnlyList<AlertSiteGroup>> GetAlertsAsync(
        string pasUserId,
        long startDateTimeUtc,
        long endDateTimeUtc,
        CancellationToken cancellationToken = default)
    {
        RawAlertsResponse? raw = !string.Equals(_options.DataSource, "live", StringComparison.OrdinalIgnoreCase)
            ? await ReadStubFileAsync(cancellationToken)
            : await GetLiveAsync(pasUserId, startDateTimeUtc, endDateTimeUtc, cancellationToken);

        return MapToSiteGroups(raw);
    }

    private async Task<RawAlertsResponse?> ReadStubFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(_options.StubFilePath, cancellationToken);
            return JsonSerializer.Deserialize<RawAlertsResponse>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogError(ex, "Failed to read alerts stub file at {Path}", _options.StubFilePath);
            throw new AlertsApiException($"Failed to read alerts stub file at '{_options.StubFilePath}'.", ex);
        }
    }

    private async Task<RawAlertsResponse?> GetLiveAsync(
        string pasUserId, long startDateTimeUtc, long endDateTimeUtc, CancellationToken cancellationToken)
    {
        var query = "api/alerts/v1/ByUserIdAndTimeRange" +
                    $"?startDateTimeUtc={startDateTimeUtc.ToString(CultureInfo.InvariantCulture)}" +
                    $"&enddatetimeutc={endDateTimeUtc.ToString(CultureInfo.InvariantCulture)}" +
                    "&compact=true&includeUserAuditEvents=true" +
                    $"&userId={Uri.EscapeDataString(pasUserId)}";

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Calling alerts API: GET {Url} (attempt {Attempt}/{MaxAttempts})",
                    new Uri(_httpClient.BaseAddress!, query), attempt, MaxAttempts);

                var response = await _httpClient.GetAsync(query, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new AlertsApiException(
                        $"Alerts API returned status {(int)response.StatusCode} ({response.StatusCode}).");
                }

                return await response.Content.ReadFromJsonAsync<RawAlertsResponse>(cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Alerts API attempt {Attempt}/{MaxAttempts} failed", attempt, MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    throw new AlertsApiException("Failed to reach the alerts API after all retry attempts.", ex);
                }

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new AlertsApiException("Failed to reach the alerts API.");
    }

    private static IReadOnlyList<AlertSiteGroup> MapToSiteGroups(RawAlertsResponse? raw)
    {
        if (raw?.Sites is null)
        {
            return Array.Empty<AlertSiteGroup>();
        }

        return raw.Sites
            .Select(site => new AlertSiteGroup
            {
                SiteName = site.Name ?? string.Empty,
                Alerts = (site.Alerts ?? new List<RawAlert>())
                    .Select(a => new AlertRecord
                    {
                        Atn = a.Atn ?? string.Empty,
                        Message = a.Message ?? string.Empty,
                        ActionUtc = a.ActionUtc,
                        IssuedUtc = a.IssuedUtc,
                        ExpiresUtc = a.ExpiresUtc,
                        AutoClearUtc = a.AutoClearUtc,
                        LastModifiedUtc = a.LastModifiedUtc
                    })
                    .ToList()
            })
            .ToList();
    }

    // Raw upstream shapes — intentionally not public domain models (BR-10: only
    // Atn/Message/5 UTC fields are ever surfaced; everything else upstream is
    // deserialized here and discarded during mapping).
    private class RawAlertsResponse
    {
        [JsonPropertyName("r")]
        public List<RawSite>? Sites { get; set; }
    }

    private class RawSite
    {
        [JsonPropertyName("nm")]
        public string? Name { get; set; }

        [JsonPropertyName("a")]
        public List<RawAlert>? Alerts { get; set; }
    }

    private class RawAlert
    {
        [JsonPropertyName("atn")]
        public string? Atn { get; set; }

        [JsonPropertyName("m")]
        public string? Message { get; set; }

        [JsonPropertyName("actutc")]
        public DateTime? ActionUtc { get; set; }

        [JsonPropertyName("issutc")]
        public DateTime? IssuedUtc { get; set; }

        [JsonPropertyName("exputc")]
        public DateTime? ExpiresUtc { get; set; }

        [JsonPropertyName("acutc")]
        public DateTime? AutoClearUtc { get; set; }

        [JsonPropertyName("lmutc")]
        public DateTime? LastModifiedUtc { get; set; }
    }
}

public class AlertsApiException : Exception
{
    public AlertsApiException(string message) : base(message) { }
    public AlertsApiException(string message, Exception innerException) : base(message, innerException) { }
}
