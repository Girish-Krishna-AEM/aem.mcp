using Xunit;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningMcpServer.ExternalApis;
using LightningCommon;

namespace LightningMcpServer.Tests.DomainModules;

public class InformerStatusModuleTests
{
    private static List<InformerRecord> SampleInformers() => new()
    {
        new InformerRecord
        {
            CustId = 287, InformId = 2049, MacAddress = "00:40:9D:D4:D1:F8",
            InformName = "QA Informer 6", PasUserId = "salesforce_oas_001.2049",
            IsDisabled = false, CustName = "AACS Lakeside High School Campus"
        },
        new InformerRecord
        {
            CustId = 287, InformId = 4089, MacAddress = "00:00:00:00:00:00",
            InformName = "test oct 15", PasUserId = "salesforce_oas_001.4089",
            IsDisabled = false, CustName = "AACS Lakeside High School Campus"
        },
        new InformerRecord
        {
            CustId = 500, InformId = 9000, MacAddress = "AA:BB:CC:DD:EE:FF",
            InformName = "Disabled Unit", PasUserId = "salesforce_oas_002.9000",
            IsDisabled = true, CustName = "Some Other Customer"
        }
    };

    private static InformerStatusModule CreateModule(
        out Mock<IInformerApiClient> informerApiMock,
        out Mock<IAlertsApiClient> alertsApiMock,
        List<InformerRecord>? informers = null)
    {
        informerApiMock = new Mock<IInformerApiClient>();
        informerApiMock
            .Setup(c => c.GetInformersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(informers ?? SampleInformers());

        alertsApiMock = new Mock<IAlertsApiClient>();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<InformerStatusModule>>();

        return new InformerStatusModule(informerApiMock.Object, alertsApiMock.Object, cache, loggerMock.Object);
    }

    [Fact]
    public void GetInformerStatus_SingleEnabledMatch_ReturnsFoundWithAlerts()
    {
        var module = CreateModule(out _, out var alertsApiMock);
        var expectedAlerts = new List<AlertSiteGroup>
        {
            new() { SiteName = "Main Site", Alerts = new List<AlertRecord> { new() { Atn = "LX", Message = "test" } } }
        };
        alertsApiMock
            .Setup(c => c.GetAlertsAsync("salesforce_oas_001.2049", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        var result = module.GetInformerStatus(new GetInformerStatusRequest { Search = "QA Informer 6" });

        Assert.Equal(InformerStatusResultKind.Found, result.Kind);
        Assert.Equal(2049, result.Match!.InformId);
        Assert.Single(result.AlertSiteGroups);
        Assert.Equal("Main Site", result.AlertSiteGroups[0].SiteName);
    }

    [Fact]
    public void GetInformerStatus_SingleEnabledMatch_NoAlertsInRange_ReturnsFoundWithEmptyGroups()
    {
        var module = CreateModule(out _, out var alertsApiMock);
        alertsApiMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSiteGroup>());

        var result = module.GetInformerStatus(new GetInformerStatusRequest { Search = "QA Informer 6" });

        Assert.Equal(InformerStatusResultKind.Found, result.Kind);
        Assert.Empty(result.AlertSiteGroups);
    }

    [Fact]
    public void GetInformerStatus_SingleDisabledMatch_ReturnsDisabledAndNeverCallsAlertsApi()
    {
        var module = CreateModule(out _, out var alertsApiMock);

        var result = module.GetInformerStatus(new GetInformerStatusRequest { Search = "Disabled Unit" });

        Assert.Equal(InformerStatusResultKind.Disabled, result.Kind);
        Assert.Equal(9000, result.Match!.InformId);
        alertsApiMock.Verify(
            c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GetInformerStatus_ZeroMatches_ReturnsNotFound()
    {
        var module = CreateModule(out _, out _);

        var result = module.GetInformerStatus(new GetInformerStatusRequest { Search = "no such informer" });

        Assert.Equal(InformerStatusResultKind.NotFound, result.Kind);
    }

    [Fact]
    public void GetInformerStatus_MultipleMatches_ReturnsAmbiguousWithAllCandidates()
    {
        var module = CreateModule(out _, out _);

        var result = module.GetInformerStatus(new GetInformerStatusRequest { Search = "AACS Lakeside High School Campus" });

        Assert.Equal(InformerStatusResultKind.Ambiguous, result.Kind);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void GetInformerStatus_NoTimeRangeSpecified_DefaultsToLast24Hours()
    {
        var module = CreateModule(out _, out var alertsApiMock);
        long? capturedStart = null, capturedEnd = null;
        alertsApiMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<string, long, long, CancellationToken>((_, s, e, _) => { capturedStart = s; capturedEnd = e; })
            .ReturnsAsync(new List<AlertSiteGroup>());

        module.GetInformerStatus(new GetInformerStatusRequest { Search = "QA Informer 6" });

        Assert.NotNull(capturedStart);
        Assert.NotNull(capturedEnd);
        Assert.Equal(TimeSpan.FromHours(24).TotalSeconds, capturedEnd!.Value - capturedStart!.Value, 2);
    }

    [Fact]
    public void GetInformerStatus_RepeatedCalls_InformerListCacheHitSkipsSecondFetch()
    {
        var module = CreateModule(out var informerApiMock, out var alertsApiMock);
        alertsApiMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSiteGroup>());

        module.GetInformerStatus(new GetInformerStatusRequest { Search = "QA Informer 6" });
        module.GetInformerStatus(new GetInformerStatusRequest { Search = "QA Informer 6" });

        informerApiMock.Verify(c => c.GetInformersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetInformerStatus_EmptySearch_ThrowsArgumentException()
    {
        var module = CreateModule(out _, out _);

        Assert.Throws<ArgumentException>(() => module.GetInformerStatus(new GetInformerStatusRequest { Search = "" }));
    }

    private static List<InformerRecord> GeoSampleInformers() => new()
    {
        // ~3.5 miles from (0,0)
        new InformerRecord
        {
            CustId = 1, InformId = 1001, MacAddress = "00:00:00:00:00:01",
            InformName = "Near Informer", PasUserId = "user.1001",
            IsDisabled = false, CustName = "Near Customer", Latitude = 0.05, Longitude = 0.0
        },
        // ~13.8 miles from (0,0)
        new InformerRecord
        {
            CustId = 2, InformId = 1002, MacAddress = "00:00:00:00:00:02",
            InformName = "Far Informer", PasUserId = "user.1002",
            IsDisabled = false, CustName = "Far Customer", Latitude = 0.2, Longitude = 0.0
        },
        // disabled, in range (~6.9 miles)
        new InformerRecord
        {
            CustId = 3, InformId = 1003, MacAddress = "00:00:00:00:00:03",
            InformName = "Disabled In Range", PasUserId = "user.1003",
            IsDisabled = true, CustName = "Disabled Customer", Latitude = 0.1, Longitude = 0.0
        },
        // not geocoded — must be excluded even though (0,0) would otherwise be "in range" of itself
        new InformerRecord
        {
            CustId = 4, InformId = 1004, MacAddress = "00:00:00:00:00:04",
            InformName = "Not Geocoded", PasUserId = "user.1004",
            IsDisabled = false, CustName = "Ungeocoded Customer", Latitude = 0.0, Longitude = 0.0
        },
        // far outside any reasonable test radius
        new InformerRecord
        {
            CustId = 5, InformId = 1005, MacAddress = "00:00:00:00:00:05",
            InformName = "Way Far Informer", PasUserId = "user.1005",
            IsDisabled = false, CustName = "Way Far Customer", Latitude = 10.0, Longitude = 10.0
        }
    };

    [Fact]
    public async Task GetInformersNearLocationAsync_ExcludesNonGeocodedAndOutOfRadiusRecords()
    {
        var module = CreateModule(out _, out var alertsApiMock, GeoSampleInformers());
        alertsApiMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSiteGroup>());

        var response = await module.GetInformersNearLocationAsync(new GetInformersNearLocationRequest
        {
            Latitude = 0, Longitude = 0, Radius = 20, RadiusUnit = "miles"
        });

        var ids = response.Informers.Select(i => i.InformId).ToList();
        Assert.Contains(1001, ids);
        Assert.Contains(1002, ids);
        Assert.Contains(1003, ids);
        Assert.DoesNotContain(1004, ids); // not geocoded, excluded
        Assert.DoesNotContain(1005, ids); // outside radius
        Assert.Equal(3, response.TotalMatchesInRadius);
        Assert.False(response.Truncated);
    }

    [Fact]
    public async Task GetInformersNearLocationAsync_DisabledInformer_IncludedButNeverCallsAlertsApi()
    {
        var module = CreateModule(out _, out var alertsApiMock, GeoSampleInformers());
        alertsApiMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSiteGroup>());

        var response = await module.GetInformersNearLocationAsync(new GetInformersNearLocationRequest
        {
            Latitude = 0, Longitude = 0, Radius = 20, RadiusUnit = "miles"
        });

        var disabled = response.Informers.Single(i => i.InformId == 1003);
        Assert.True(disabled.IsDisabled);
        Assert.Equal(0, disabled.AlertCount);
        Assert.Equal(0, disabled.SiteCount);
        alertsApiMock.Verify(
            c => c.GetAlertsAsync("user.1003", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetInformersNearLocationAsync_MoreThan25InRadius_TruncatesToNearest25()
    {
        var many = new List<InformerRecord>();
        for (var i = 1; i <= 30; i++)
        {
            many.Add(new InformerRecord
            {
                CustId = i, InformId = 2000 + i, MacAddress = $"00:00:00:00:00:{i:D2}",
                InformName = $"Informer {i}", PasUserId = $"user.{2000 + i}",
                IsDisabled = false, CustName = "Dense Customer",
                Latitude = 0.001 * i, Longitude = 0.0 // all well within radius, increasing distance
            });
        }

        var module = CreateModule(out _, out var alertsApiMock, many);
        alertsApiMock
            .Setup(c => c.GetAlertsAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSiteGroup>());

        var response = await module.GetInformersNearLocationAsync(new GetInformersNearLocationRequest
        {
            Latitude = 0, Longitude = 0, Radius = 50, RadiusUnit = "miles"
        });

        Assert.Equal(30, response.TotalMatchesInRadius);
        Assert.True(response.Truncated);
        Assert.Equal(25, response.Informers.Count);
        // nearest 25 (i = 1..25) should be the ones returned
        Assert.All(response.Informers, m => Assert.True(m.InformId <= 2025));
    }

    [Fact]
    public async Task GetInformersNearLocationAsync_OneAlertsCallThrows_OtherInformersStillSucceed()
    {
        var module = CreateModule(out _, out var alertsApiMock, GeoSampleInformers());
        alertsApiMock
            .Setup(c => c.GetAlertsAsync("user.1001", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AlertsApiException("simulated failure"));
        alertsApiMock
            .Setup(c => c.GetAlertsAsync("user.1002", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertSiteGroup>
            {
                new() { SiteName = "Site", Alerts = new List<AlertRecord> { new() { Atn = "LX", Message = "m" } } }
            });

        var response = await module.GetInformersNearLocationAsync(new GetInformersNearLocationRequest
        {
            Latitude = 0, Longitude = 0, Radius = 20, RadiusUnit = "miles"
        });

        var failed = response.Informers.Single(i => i.InformId == 1001);
        Assert.Equal(0, failed.AlertCount);
        Assert.Equal(0, failed.SiteCount);

        var succeeded = response.Informers.Single(i => i.InformId == 1002);
        Assert.Equal(1, succeeded.AlertCount);
        Assert.Equal(1, succeeded.SiteCount);
    }

    [Fact]
    public async Task GetInformersNearLocationAsync_RadiusOverMax_ThrowsArgumentException()
    {
        var module = CreateModule(out _, out _, GeoSampleInformers());

        await Assert.ThrowsAsync<ArgumentException>(() => module.GetInformersNearLocationAsync(new GetInformersNearLocationRequest
        {
            Latitude = 0, Longitude = 0, Radius = 150, RadiusUnit = "miles"
        }));
    }

    [Fact]
    public async Task GetInformersNearLocationAsync_ZeroInRadius_ReturnsEmptyList()
    {
        var module = CreateModule(out _, out _, GeoSampleInformers());

        var response = await module.GetInformersNearLocationAsync(new GetInformersNearLocationRequest
        {
            Latitude = 50, Longitude = 50, Radius = 10, RadiusUnit = "miles"
        });

        Assert.Empty(response.Informers);
        Assert.Equal(0, response.TotalMatchesInRadius);
        Assert.False(response.Truncated);
    }
}
