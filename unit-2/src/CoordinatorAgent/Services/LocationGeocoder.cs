using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CoordinatorAgent.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CoordinatorAgent.Services;

public class LocationGeocoder : ILocationGeocoder
{
    public const string NominatimHttpClientName = "NominatimGeocoder";

    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly HttpClient _censusHttpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocationGeocoder> _logger;

    public LocationGeocoder(
        HttpClient censusHttpClient,
        IHttpClientFactory httpClientFactory,
        IOptions<CensusGeocoderOptions> censusOptions,
        IMemoryCache cache,
        ILogger<LocationGeocoder> logger)
    {
        _censusHttpClient = censusHttpClient;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;

        if (_censusHttpClient.BaseAddress is null)
        {
            _censusHttpClient.BaseAddress = new Uri(censusOptions.Value.BaseUrl);
        }

        _censusHttpClient.Timeout = TimeSpan.FromSeconds(censusOptions.Value.TimeoutSeconds);
    }

    public async Task<GeocodeOutcome> GeocodeAsync(string locationText, CancellationToken cancellationToken = default)
    {
        var cacheKey = NormalizeCacheKey(locationText);

        if (_cache.TryGetValue(cacheKey, out GeocodeOutcome? cached) && cached is not null)
        {
            _logger.LogInformation("Geocode cache hit for '{LocationText}'", locationText);
            return cached;
        }

        _logger.LogInformation("Geocode cache miss for '{LocationText}', calling Census Geocoder", locationText);

        var censusMatches = await CallWithRetryAsync(
            () => _censusHttpClient.GetFromJsonAsync<CensusGeocoderResponse>(
                $"locations/onelineaddress?address={Uri.EscapeDataString(locationText)}&benchmark=Public_AR_Current&format=json",
                cancellationToken),
            response => response?.Result?.AddressMatches?.Select(m => (m.MatchedAddress, m.Coordinates.Y, m.Coordinates.X)).ToList()
                        ?? new List<(string, double, double)>(),
            locationText,
            "Census Geocoder",
            cancellationToken);

        GeocodeOutcome outcome;

        if (censusMatches.Count > 0)
        {
            outcome = ToOutcome(censusMatches, locationText);
        }
        else
        {
            _logger.LogInformation("Census Geocoder found no match for '{LocationText}', falling back to Nominatim", locationText);
            outcome = await GeocodeWithNominatimAsync(locationText, cancellationToken);
        }

        _cache.Set(cacheKey, outcome, CacheTtl);

        return outcome;
    }

    private async Task<GeocodeOutcome> GeocodeWithNominatimAsync(string locationText, CancellationToken cancellationToken)
    {
        var nominatimClient = _httpClientFactory.CreateClient(NominatimHttpClientName);

        var nominatimMatches = await CallWithRetryAsync(
            // limit=1: trust Nominatim's own relevance ranking (it already factors in place importance/population)
            // and take its top match directly, rather than surfacing same-state sub-localities (e.g. multiple
            // "Germantown, MD" neighborhoods) as a false-ambiguous prompt. Genuine ambiguity (e.g. a bare place
            // name matching different states) is resolved silently in Nominatim's favor of its top-ranked result.
            () => nominatimClient.GetFromJsonAsync<List<NominatimMatch>>(
                $"search?q={Uri.EscapeDataString(locationText)}&format=json&limit=1&countrycodes=us",
                cancellationToken),
            response => response?.Select(m => (m.DisplayName, ParseDouble(m.Lat), ParseDouble(m.Lon))).ToList()
                        ?? new List<(string, double, double)>(),
            locationText,
            "Nominatim",
            cancellationToken);

        return nominatimMatches.Count == 0 ? GeocodeOutcome.NotFound : ToOutcome(nominatimMatches, locationText);
    }

    private static GeocodeOutcome ToOutcome(List<(string MatchedAddress, double Latitude, double Longitude)> matches, string locationText)
    {
        if (matches.Count > 1)
        {
            return GeocodeOutcome.Ambiguous(matches.Select(m => m.MatchedAddress).ToList());
        }

        var match = matches[0];
        return GeocodeOutcome.Resolved(new GeocodeResult
        {
            Latitude = match.Latitude,
            Longitude = match.Longitude,
            MatchedAddress = match.MatchedAddress
        });
    }

    private async Task<List<(string, double, double)>> CallWithRetryAsync<TResponse>(
        Func<Task<TResponse?>> call,
        Func<TResponse?, List<(string, double, double)>> mapMatches,
        string locationText,
        string providerName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var response = await call();
                return mapMatches(response);
            }
            catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "{ProviderName} attempt {Attempt}/{MaxAttempts} failed for '{LocationText}', retrying in {DelayMs}ms",
                    providerName, attempt, MaxAttempts, locationText, RetryDelay.TotalMilliseconds);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        // Unreachable: on the final attempt (attempt == MaxAttempts), the catch filter above
        // excludes it, so a failing final attempt propagates its own exception directly.
        throw new InvalidOperationException($"{providerName} retry loop exited without returning or throwing.");
    }

    private static double ParseDouble(string value) =>
        double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static string NormalizeCacheKey(string locationText) => locationText.Trim().ToLowerInvariant();

    private class CensusGeocoderResponse
    {
        [JsonPropertyName("result")]
        public CensusGeocoderResult? Result { get; set; }
    }

    private class CensusGeocoderResult
    {
        [JsonPropertyName("addressMatches")]
        public List<CensusAddressMatch>? AddressMatches { get; set; }
    }

    private class CensusAddressMatch
    {
        [JsonPropertyName("matchedAddress")]
        public string MatchedAddress { get; set; } = string.Empty;

        [JsonPropertyName("coordinates")]
        public CensusCoordinates Coordinates { get; set; } = new();
    }

    private class CensusCoordinates
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }

    private class NominatimMatch
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("lat")]
        public string Lat { get; set; } = "0";

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = "0";
    }
}
