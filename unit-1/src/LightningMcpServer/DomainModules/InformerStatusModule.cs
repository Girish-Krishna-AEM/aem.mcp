using LightningCommon;
using LightningMcpServer.ExternalApis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace LightningMcpServer.DomainModules;

public class InformerStatusModule : IInformerStatusModule
{
    private const string InformerListCacheKey = "informer-list";
    private static readonly TimeSpan InformerListCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan DefaultTimeRangeWindow = TimeSpan.FromHours(24);

    private readonly IInformerApiClient _informerApiClient;
    private readonly IAlertsApiClient _alertsApiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InformerStatusModule> _logger;

    public InformerStatusModule(
        IInformerApiClient informerApiClient,
        IAlertsApiClient alertsApiClient,
        IMemoryCache cache,
        ILogger<InformerStatusModule> logger)
    {
        _informerApiClient = informerApiClient;
        _alertsApiClient = alertsApiClient;
        _cache = cache;
        _logger = logger;
    }

    public InformerStatusResult GetInformerStatus(GetInformerStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Search))
        {
            throw new ArgumentException("Search text must be provided.");
        }

        var informers = GetInformerList();
        var matches = SearchInformers(informers, request.Search);

        if (matches.Count == 0)
        {
            _logger.LogWarning("No informer found matching search '{Search}'", request.Search);
            return InformerStatusResult.NotFound;
        }

        if (matches.Count > 1)
        {
            _logger.LogWarning(
                "Ambiguous informer search '{Search}' matched {Count} informers", request.Search, matches.Count);
            return InformerStatusResult.Ambiguous(matches.Select(ToSearchMatch).ToList());
        }

        var matchedRecord = matches[0];
        var match = ToSearchMatch(matchedRecord);

        if (matchedRecord.IsDisabled)
        {
            _logger.LogWarning("Informer '{InformName}' (ID {InformId}) is disabled", match.InformName, match.InformId);
            return InformerStatusResult.Disabled(match);
        }

        var (start, end) = ResolveTimeRange(request.StartDateTimeUtc, request.EndDateTimeUtc);

        _logger.LogInformation(
            "Fetching alerts for informer '{InformName}' (ID {InformId}) between {Start} and {End}",
            match.InformName, match.InformId, start, end);

        var alertSiteGroups = _alertsApiClient
            .GetAlertsAsync(matchedRecord.PasUserId, start, end)
            .GetAwaiter()
            .GetResult();

        return InformerStatusResult.Found(match, alertSiteGroups);
    }

    private IReadOnlyList<InformerRecord> GetInformerList()
    {
        if (_cache.TryGetValue(InformerListCacheKey, out IReadOnlyList<InformerRecord>? cached) && cached is not null)
        {
            _logger.LogInformation("Informer list cache hit ({Count} informers)", cached.Count);
            return cached;
        }

        var list = _informerApiClient.GetInformersAsync().GetAwaiter().GetResult();
        _logger.LogInformation("Informer list cache miss — fetched {Count} informers", list.Count);
        _cache.Set(InformerListCacheKey, list, InformerListCacheTtl);
        return list;
    }

    private static List<InformerRecord> SearchInformers(IReadOnlyList<InformerRecord> informers, string search) =>
        informers
            .Where(i =>
                Contains(i.CustName, search) ||
                Contains(i.InformName, search) ||
                Contains(i.MacAddress, search) ||
                Contains(i.InformId.ToString(), search))
            .ToList();

    private static bool Contains(string field, string search) =>
        field.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static InformerSearchMatch ToSearchMatch(InformerRecord record) => new()
    {
        InformId = record.InformId,
        InformName = record.InformName,
        MacAddress = record.MacAddress,
        CustName = record.CustName,
        IsDisabled = record.IsDisabled
    };

    private static (long Start, long End) ResolveTimeRange(long? startDateTimeUtc, long? endDateTimeUtc)
    {
        if (startDateTimeUtc.HasValue && endDateTimeUtc.HasValue)
        {
            return (startDateTimeUtc.Value, endDateTimeUtc.Value);
        }

        var end = DateTimeOffset.UtcNow;
        var start = end - DefaultTimeRangeWindow;
        return (start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
    }
}
