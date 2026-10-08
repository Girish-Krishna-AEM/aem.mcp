using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningMcpServer.ExternalApis;
using LightningCommon;

namespace LightningMcpServer.Tests.DomainModules;

public class StrikeDetectionModuleTests
{
    private readonly Mock<ILogger<StrikeDetectionModule>> _loggerMock;
    private readonly Mock<ILightningPulseApiClient> _apiClientMock;
    private readonly StrikeDetectionModule _module;

    public StrikeDetectionModuleTests()
    {
        _loggerMock = new Mock<ILogger<StrikeDetectionModule>>();
        _apiClientMock = new Mock<ILightningPulseApiClient>();
        _module = new StrikeDetectionModule(_loggerMock.Object, _apiClientMock.Object);
    }

    private static readonly GetLightningStrikesNearLocationRequest AustinRequest = new()
    {
        Latitude = 30.2672,
        Longitude = -97.7431,
        Radius = 50,
        RadiusUnit = "km"
    };

    private static readonly GetLightningStrikesNearLocationRequest AustinRequestWithDetails = new()
    {
        Latitude = 30.2672,
        Longitude = -97.7431,
        Radius = 50,
        RadiusUnit = "km",
        IncludeDetails = true
    };

    private void SetupPulses(params PulseDto[] pulses)
    {
        _apiClientMock
            .Setup(c => c.GetPulsesAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PulsesApiResponse { Pulses = pulses.ToList() });
    }

    [Fact]
    public void GetLightningStrikesNearLocation_MapsLatLonTypeAndIntensity()
    {
        var timestamp = new DateTime(2025, 10, 5, 19, 43, 7, DateTimeKind.Utc);
        SetupPulses(new PulseDto
        {
            Lat = 30.27,
            Lon = -97.74,
            Cur = 8.146,
            Typ = 0,
            Ts = timestamp
        });

        var response = _module.GetLightningStrikesNearLocation(AustinRequestWithDetails);

        Assert.Equal(1, response.StrikeCount);
        var strike = Assert.Single(response.Strikes);
        Assert.Equal(30.27, strike.Latitude);
        Assert.Equal(-97.74, strike.Longitude);
        Assert.Equal(8.146, strike.Intensity);
        Assert.Equal("cg", strike.Type);
        Assert.Equal(timestamp, strike.Timestamp);
    }

    [Theory]
    [InlineData(0, "cg")]
    [InlineData(1, "ic")]
    public void GetLightningStrikesNearLocation_MapsPulseTypeToCgOrIc(int typ, string expectedType)
    {
        SetupPulses(new PulseDto { Lat = 30.27, Lon = -97.74, Cur = 1.0, Typ = typ, Ts = DateTime.UtcNow });

        var response = _module.GetLightningStrikesNearLocation(AustinRequestWithDetails);

        Assert.Equal(expectedType, response.Strikes[0].Type);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_FiltersPulsesOutsideRadius()
    {
        SetupPulses(
            new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 1.0, Typ = 0, Ts = DateTime.UtcNow }, // ~0km
            new PulseDto { Lat = 40.7128, Lon = -74.0060, Cur = 2.0, Typ = 0, Ts = DateTime.UtcNow }   // far away (NYC)
        );

        var response = _module.GetLightningStrikesNearLocation(AustinRequestWithDetails);

        Assert.Equal(1, response.StrikeCount);
        Assert.Equal(30.2672, response.Strikes[0].Latitude);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithIncludeDetailsFalse_OmitsStrikesButKeepsCounts()
    {
        SetupPulses(
            new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 5.0, Typ = 0, Ts = DateTime.UtcNow },
            new PulseDto { Lat = 30.2673, Lon = -97.7432, Cur = 3.0, Typ = 1, Ts = DateTime.UtcNow }
        );

        var response = _module.GetLightningStrikesNearLocation(AustinRequest);

        Assert.Equal(2, response.StrikeCount);
        Assert.Empty(response.Strikes);
        Assert.Equal(1, response.CloudToGroundCount);
        Assert.Equal(1, response.IntraCloudCount);
        Assert.Equal(5.0, response.MaxIntensity);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_ComputesCgIcCountsAndMaxIntensityFromFullSet()
    {
        SetupPulses(
            new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 5.0, Typ = 0, Ts = DateTime.UtcNow },
            new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 9.5, Typ = 0, Ts = DateTime.UtcNow },
            new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 2.0, Typ = 1, Ts = DateTime.UtcNow }
        );

        var response = _module.GetLightningStrikesNearLocation(AustinRequestWithDetails);

        Assert.Equal(3, response.StrikeCount);
        Assert.Equal(2, response.CloudToGroundCount);
        Assert.Equal(1, response.IntraCloudCount);
        Assert.Equal(9.5, response.MaxIntensity);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithMoreThan25Matches_TruncatesDetailsToTop25ByIntensityButKeepsFullCount()
    {
        var pulses = Enumerable.Range(1, 30)
            .Select(i => new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = i, Typ = 0, Ts = DateTime.UtcNow })
            .ToArray();
        SetupPulses(pulses);

        var response = _module.GetLightningStrikesNearLocation(AustinRequestWithDetails);

        Assert.Equal(30, response.StrikeCount);
        Assert.Equal(25, response.Strikes.Count);
        Assert.Equal(30.0, response.MaxIntensity);
        Assert.Equal(30.0, response.Strikes[0].Intensity);
        Assert.True(response.Strikes[0].Intensity >= response.Strikes[^1].Intensity);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WhenApiReturnsNullPulses_ReturnsEmptyResult()
    {
        // Real QA API returns "pulses": null (not []) when there are no records in range.
        _apiClientMock
            .Setup(c => c.GetPulsesAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PulsesApiResponse { Code = 204, Pulses = null! });

        var response = _module.GetLightningStrikesNearLocation(AustinRequest);

        Assert.Equal(0, response.StrikeCount);
        Assert.Empty(response.Strikes);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithNoPulsesInRadius_ReturnsEmptyResult()
    {
        SetupPulses(new PulseDto { Lat = 40.7128, Lon = -74.0060, Cur = 2.0, Typ = 0, Ts = DateTime.UtcNow });

        var response = _module.GetLightningStrikesNearLocation(AustinRequest);

        Assert.Equal(0, response.StrikeCount);
        Assert.Empty(response.Strikes);
        Assert.Equal(0, response.NearestStrikeDistance);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithInvalidRadius_ThrowsArgumentException()
    {
        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            Radius = -10,
            RadiusUnit = "km"
        };

        Assert.Throws<ArgumentException>(() => _module.GetLightningStrikesNearLocation(request));
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithInvalidRadiusUnit_ThrowsArgumentException()
    {
        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            Radius = 50,
            RadiusUnit = "feet"
        };

        Assert.Throws<ArgumentException>(() => _module.GetLightningStrikesNearLocation(request));
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithInvalidPulseType_ThrowsArgumentException()
    {
        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            Radius = 50,
            RadiusUnit = "km",
            PulseType = "bogus"
        };

        Assert.Throws<ArgumentException>(() => _module.GetLightningStrikesNearLocation(request));
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithStartAndEndDateTime_ForwardsExactDatesToApiClient()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        SetupPulses(new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 1.0, Typ = 0, Ts = DateTime.UtcNow });

        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            Radius = 50,
            RadiusUnit = "km",
            StartDateTime = start,
            EndDateTime = end
        };

        _module.GetLightningStrikesNearLocation(request);

        _apiClientMock.Verify(c => c.GetPulsesAsync(start, end, null, 30.2672, -97.7431, 50, "km", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("CG", 0)]
    [InlineData("CloudToGround", 0)]
    [InlineData("IC", 1)]
    [InlineData("IntraCloud", 1)]
    public void GetLightningStrikesNearLocation_WithPulseTypeAndDateRange_ForwardsMappedTypeAndDatesToApiClient(
        string pulseType, int expectedMappedType)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        SetupPulses(new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 1.0, Typ = expectedMappedType, Ts = DateTime.UtcNow });

        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            Radius = 50,
            RadiusUnit = "km",
            StartDateTime = start,
            EndDateTime = end,
            PulseType = pulseType
        };

        _module.GetLightningStrikesNearLocation(request);

        _apiClientMock.Verify(c => c.GetPulsesAsync(start, end, expectedMappedType, 30.2672, -97.7431, 50, "km", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WithNoDateRangeOrPulseType_ForwardsNullsToApiClient()
    {
        SetupPulses(new PulseDto { Lat = 30.2672, Lon = -97.7431, Cur = 1.0, Typ = 0, Ts = DateTime.UtcNow });

        _module.GetLightningStrikesNearLocation(AustinRequest);

        _apiClientMock.Verify(
            c => c.GetPulsesAsync(null, null, null, 30.2672, -97.7431, 50, "km", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_ForwardsLatitudeLongitudeRadiusAndUnitToApiClient()
    {
        SetupPulses(new PulseDto { Lat = 25.7907, Lon = -80.1300, Cur = 1.0, Typ = 0, Ts = DateTime.UtcNow });

        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = 25.7907,
            Longitude = -80.1300,
            Radius = 100,
            RadiusUnit = "miles"
        };

        _module.GetLightningStrikesNearLocation(request);

        _apiClientMock.Verify(
            c => c.GetPulsesAsync(
                null, null, null, 25.7907, -80.1300, 100, "miles", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetLightningStrikesNearLocation_WhenApiClientThrows_PropagatesException()
    {
        _apiClientMock
            .Setup(c => c.GetPulsesAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LightningPulseApiException("Lightning pulse API returned status 500 (InternalServerError)."));

        Assert.Throws<LightningPulseApiException>(() => _module.GetLightningStrikesNearLocation(AustinRequest));
    }
}
