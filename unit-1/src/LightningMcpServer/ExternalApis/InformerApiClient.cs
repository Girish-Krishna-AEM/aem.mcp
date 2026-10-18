using System.Net.Http.Json;
using System.Text.Json;
using LightningCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LightningMcpServer.ExternalApis;

public class InformerApiClient : IInformerApiClient
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _httpClient;
    private readonly InformerApiOptions _options;
    private readonly ILogger<InformerApiClient> _logger;

    public InformerApiClient(
        HttpClient httpClient,
        IOptions<InformerApiOptions> options,
        ILogger<InformerApiClient> logger)
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

    public async Task<IReadOnlyList<InformerRecord>> GetInformersAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_options.DataSource, "live", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadStubFileAsync(cancellationToken);
        }

        return await GetLiveAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InformerRecord>> ReadStubFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(_options.StubFilePath, cancellationToken);
            var records = JsonSerializer.Deserialize<List<InformerRecord>>(json);
            return records ?? new List<InformerRecord>();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogError(ex, "Failed to read informer list stub file at {Path}", _options.StubFilePath);
            throw new InformerApiException($"Failed to read informer list stub file at '{_options.StubFilePath}'.", ex);
        }
    }

    private async Task<IReadOnlyList<InformerRecord>> GetLiveAsync(CancellationToken cancellationToken)
    {
        const string query = "api/oas/Informer/v1/GetInformers";

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Calling informer-list API: GET {Url} (attempt {Attempt}/{MaxAttempts})",
                    new Uri(_httpClient.BaseAddress!, query), attempt, MaxAttempts);

                var response = await _httpClient.GetAsync(query, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InformerApiException(
                        $"Informer-list API returned status {(int)response.StatusCode} ({response.StatusCode}).");
                }

                var records = await response.Content.ReadFromJsonAsync<List<InformerRecord>>(cancellationToken: cancellationToken);
                return records ?? new List<InformerRecord>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Informer-list API attempt {Attempt}/{MaxAttempts} failed", attempt, MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    throw new InformerApiException("Failed to reach the informer-list API after all retry attempts.", ex);
                }

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        throw new InformerApiException("Failed to reach the informer-list API.");
    }
}

public class InformerApiException : Exception
{
    public InformerApiException(string message) : base(message) { }
    public InformerApiException(string message, Exception innerException) : base(message, innerException) { }
}
