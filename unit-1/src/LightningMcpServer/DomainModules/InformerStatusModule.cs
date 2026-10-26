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
    private const int MaxFanOutResults = 25;
    private const double MaxLocationSearchRadiusMiles = 100;

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

    public async Task<GetInformersNearLocationResponse> GetInformersNearLocationAsync(GetInformersNearLocationRequest request)
    {
        ValidateLocationRequest(request);

        var informers = GetInformerList();
        var radiusMiles = string.Equals(request.RadiusUnit, "km", StringComparison.OrdinalIgnoreCase)
            ? request.Radius / GeoDistanceUtils.KmPerMile
            : request.Radius;

        var inRadius = informers
            .Where(i => !(i.Latitude == 0 && i.Longitude == 0))
            .Select(i => new
            {
                Record = i,
                DistanceMiles = GeoDistanceUtils.CalculateDistanceMiles(request.Latitude, request.Longitude, i.Latitude, i.Longitude)
            })
            .Where(x => x.DistanceMiles <= radiusMiles)
            .OrderBy(x => x.DistanceMiles)
            .ToList();

        var totalMatchesInRadius = inRadius.Count;
        var truncated = totalMatchesInRadius > MaxFanOutResults;
        var nearest = inRadius.Take(MaxFanOutResults).ToList();

        var (start, end) = ResolveTimeRange(request.StartDateTimeUtc, request.EndDateTimeUtc);

        _logger.LogInformation(
            "Location-based informer search: {TotalMatches} informer(s) within {RadiusMiles} miles of ({Latitude},{Longitude}); fetching alerts for nearest {FanOutCount}",
            totalMatchesInRadius, radiusMiles, request.Latitude, request.Longitude, nearest.Count);

        var alertTasks = nearest.Select(x => FetchInformerLocationMatchAsync(x.Record, x.DistanceMiles, start, end));
        var results = await Task.WhenAll(alertTasks);

        return new GetInformersNearLocationResponse
        {
            Informers = results,
            Truncated = truncated,
            TotalMatchesInRadius = totalMatchesInRadius
        };
    }

    private async Task<InformerLocationMatch> FetchInformerLocationMatchAsync(
        InformerRecord record, double distanceMiles, long start, long end)
    {
        var match = new InformerLocationMatch
        {
            InformId = record.InformId,
            InformName = record.InformName,
            CustName = record.CustName,
            MacAddress = record.MacAddress,
            IsDisabled = record.IsDisabled,
            DistanceMiles = distanceMiles
        };

        if (record.IsDisabled)
        {
            return match;
        }

        try
        {
            var siteGroups = await _alertsApiClient.GetAlertsAsync(record.PasUserId, start, end);
            match.SiteCount = siteGroups.Count;
            match.AlertCount = siteGroups.Sum(g => g.Alerts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Alerts lookup failed for informer '{InformName}' (ID {InformId}) during location search; reporting 0 alerts",
                record.InformName, record.InformId);
        }

        return match;
    }

    private static void ValidateLocationRequest(GetInformersNearLocationRequest request)
    {
        if (double.IsNaN(request.Latitude) || double.IsNaN(request.Longitude))
        {
            throw new ArgumentException("Invalid location coordinates");
        }

        if (request.Radius <= 0)
        {
            throw new ArgumentException("Radius must be greater than 0");
        }

        if (!string.Equals(request.RadiusUnit, "km", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.RadiusUnit, "miles", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("RadiusUnit must be 'km' or 'miles'");
        }

        var radiusMiles = string.Equals(request.RadiusUnit, "km", StringComparison.OrdinalIgnoreCase)
            ? request.Radius / GeoDistanceUtils.KmPerMile
            : request.Radius;

        if (radiusMiles > MaxLocationSearchRadiusMiles)
        {
            throw new ArgumentException(
                $"Radius of {request.Radius} {request.RadiusUnit} exceeds the maximum allowed radius of {MaxLocationSearchRadiusMiles} miles.");
        }
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
