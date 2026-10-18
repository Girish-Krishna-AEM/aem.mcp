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
}
