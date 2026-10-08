using CoordinatorAgent.Models;
using CoordinatorAgent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CoordinatorAgent.Tests.Services;

public class ParameterExtractorTests
{
    private static readonly Dictionary<string, GeocodeResult> KnownGeocodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["austin, tx"] = new GeocodeResult { Latitude = 30.2672, Longitude = -97.7431, MatchedAddress = "Austin, TX" },
        ["dallas"] = new GeocodeResult { Latitude = 32.7767, Longitude = -96.7970, MatchedAddress = "Dallas, TX" },
        ["germantown md"] = new GeocodeResult { Latitude = 39.1732, Longitude = -77.2719, MatchedAddress = "Germantown, MD, 20874" }
    };

    private static readonly TimeZoneInfo UserTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private readonly Mock<ILocationGeocoder> _geocoderMock;
    private readonly ParameterExtractor _extractor;

    public ParameterExtractorTests()
    {
        _geocoderMock = new Mock<ILocationGeocoder>();
        _geocoderMock
            .Setup(g => g.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
                KnownGeocodes.TryGetValue(text.Trim(), out var result)
                    ? GeocodeOutcome.Resolved(result)
                    : GeocodeOutcome.NotFound);

        _extractor = new ParameterExtractor(NullLogger<ParameterExtractor>.Instance, _geocoderMock.Object, UserTimeZone);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithKnownCity_ReturnsDefaultRadius()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("Is there lightning near Austin, TX?", Intent.Strike);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(30.2672, parameters!.Value.GetProperty("latitude").GetDouble(), 4);
        Assert.Equal(50, parameters.Value.GetProperty("radius").GetDouble());
        Assert.Equal("km", parameters.Value.GetProperty("radiusUnit").GetString());
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithExplicitRadius_ParsesValueAndUnit()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Is there lightning near Austin, TX within 30 miles?", Intent.Strike);

        Assert.True(success);
        Assert.Equal(30, parameters!.Value.GetProperty("radius").GetDouble());
        Assert.Equal("miles", parameters.Value.GetProperty("radiusUnit").GetString());
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithNoTimeWindow_OmitsDateTimeFields()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Is there lightning near Austin, TX?", Intent.Strike);

        Assert.True(success);
        Assert.False(parameters!.Value.TryGetProperty("startDateTime", out _));
        Assert.False(parameters.Value.TryGetProperty("endDateTime", out _));
        Assert.False(parameters.Value.TryGetProperty("pulseType", out _));
    }

    [Theory]
    [InlineData("in the last 2 hours", "hours")]
    [InlineData("in the past 30 minutes", "minutes")]
    [InlineData("over the last 3 days", "days")]
    public async Task ExtractStrikeParameters_WithRelativeTimeWindow_SetsStartBeforeEnd(string window, string _)
    {
        var (success, parameters, _) = await _extractor.ExtractParameters($"Any lightning near Austin, TX {window}?", Intent.Strike);

        Assert.True(success);
        var start = parameters!.Value.GetProperty("startDateTime").GetDateTime();
        var end = parameters.Value.GetProperty("endDateTime").GetDateTime();
        Assert.True(start < end);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithYesterday_SetsFullDayBeforeToday()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Any lightning near Austin, TX yesterday?", Intent.Strike);

        Assert.True(success);
        var start = parameters!.Value.GetProperty("startDateTime").GetDateTime();
        var end = parameters.Value.GetProperty("endDateTime").GetDateTime();
        Assert.True(start < end);
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UserTimeZone).Date;
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(todayLocal.AddDays(-1), UserTimeZone), start);
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(todayLocal, UserTimeZone), end);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithExplicitDateRange_ParsesStartAndEndDateTimeConvertedFromLocalToUtc()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters(
            "Lightning near Austin, TX from 2026-01-01 to 2026-01-05", Intent.Strike);

        Assert.True(success);
        Assert.Null(error);
        var start = parameters!.Value.GetProperty("startDateTime").GetDateTime();
        var end = parameters.Value.GetProperty("endDateTime").GetDateTime();
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 1, 1), UserTimeZone), start);
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 1, 5), UserTimeZone), end);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithExplicitDateTimeRangeUsingBetweenAnd_ParsesStartAndEndDateTimeConvertedFromLocalToUtc()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters(
            "Lightning near Austin, TX between 2026-01-01T08:00:00 and 2026-01-01T20:00:00", Intent.Strike);

        Assert.True(success);
        Assert.Null(error);
        var start = parameters!.Value.GetProperty("startDateTime").GetDateTime();
        var end = parameters.Value.GetProperty("endDateTime").GetDateTime();
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 1, 1, 8, 0, 0), UserTimeZone), start);
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 1, 1, 20, 0, 0), UserTimeZone), end);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithPointPrefixAndNoAndConnector_FailsToResolveLocation()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters(
            "Lightning between 2026-06-01T11:25:03 2026-06-08T10:35:02 for 150km Point:33.77304,-75.18979",
            Intent.Strike);

        // Documents current behavior: "Point:" isn't a recognized coordinate/location format,
        // and "between X Y" without "and" doesn't match the explicit date-range pattern.
        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithLatLongKeywordsAndAndConnector_ResolvesCoordinatesRadiusAndDateRange()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters(
            "Lightning near latitude 33.77304, longitude -75.18979 within 150km between 2026-06-01T11:25:03 and 2026-06-08T10:35:02",
            Intent.Strike);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(33.77304, parameters!.Value.GetProperty("latitude").GetDouble(), 4);
        Assert.Equal(-75.18979, parameters.Value.GetProperty("longitude").GetDouble(), 4);
        Assert.Equal(150, parameters.Value.GetProperty("radius").GetDouble());
        Assert.Equal("km", parameters.Value.GetProperty("radiusUnit").GetString());
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 6, 1, 11, 25, 3), UserTimeZone), parameters.Value.GetProperty("startDateTime").GetDateTime());
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 6, 8, 10, 35, 2), UserTimeZone), parameters.Value.GetProperty("endDateTime").GetDateTime());
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithExplicitDateRangeStartAfterEnd_ReturnsValidationError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters(
            "Lightning near Austin, TX from 2026-01-05 to 2026-01-01", Intent.Strike);

        Assert.False(success);
        Assert.Null(parameters);
        Assert.Contains("start date/time must be before end date/time", error);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithUnparseableExplicitDateRange_ReturnsValidationError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters(
            "Lightning near Austin, TX from 2026-13-40 to 2026-01-05", Intent.Strike);

        Assert.False(success);
        Assert.Null(parameters);
        Assert.Contains("Could not parse date range", error);
    }

    [Theory]
    [InlineData("cloud-to-ground strikes near Austin, TX", "CG")]
    [InlineData("CG strikes near Austin, TX", "CG")]
    [InlineData("intra-cloud activity near Austin, TX", "IC")]
    [InlineData("IC strikes near Austin, TX", "IC")]
    public async Task ExtractStrikeParameters_WithPulseTypePhrase_MapsToExpectedCode(string query, string expected)
    {
        var (success, parameters, _) = await _extractor.ExtractParameters(query, Intent.Strike);

        Assert.True(success);
        Assert.Equal(expected, parameters!.Value.GetProperty("pulseType").GetString());
    }

    [Fact]
    public async Task ExtractStrikeParameters_MissingLocation_ReturnsError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("Is there lightning?", Intent.Strike);

        Assert.False(success);
        Assert.Null(parameters);
        Assert.Contains("location", error);
    }

    [Fact]
    public async Task ExtractStrikeParameters_WithFreeTextLocationAndAroundRadius_ResolvesLocationAndRadius()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("Get all the LX for Germantown MD around 50 miles", Intent.Strike);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(39.1732, parameters!.Value.GetProperty("latitude").GetDouble(), 4);
        Assert.Equal(50, parameters.Value.GetProperty("radius").GetDouble());
        Assert.Equal("miles", parameters.Value.GetProperty("radiusUnit").GetString());
    }

    [Fact]
    public async Task ExtractStrikeParameters_AmbiguousLocation_ReturnsCandidateListError()
    {
        _geocoderMock
            .Setup(g => g.GeocodeAsync("Springfield", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeocodeOutcome.Ambiguous(new[] { "Springfield, IL", "Springfield, MO" }));

        var (success, parameters, error) = await _extractor.ExtractParameters("Is there lightning near Springfield?", Intent.Strike);

        Assert.False(success);
        Assert.Null(parameters);
        Assert.Contains("Springfield, IL", error);
        Assert.Contains("Springfield, MO", error);
    }

    [Fact]
    public async Task ExtractStrikeParameters_NotFoundLocation_ReturnsNotFoundError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("Is there lightning near Nowhereville XX?", Intent.Strike);

        Assert.False(success);
        Assert.Null(parameters);
        Assert.Contains("Could not resolve location", error);
    }

    [Fact]
    public async Task ExtractStrikeParameters_RadiusExceedsCap_ReturnsValidationError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("Is there lightning near Austin, TX within 150 miles?", Intent.Strike);

        Assert.False(success);
        Assert.Null(parameters);
        Assert.Contains("exceeds the maximum allowed radius", error);
    }

    [Fact]
    public async Task ExtractWeatherParameters_Default_ReturnsDailyForecast()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("What's the weather in Dallas?", Intent.Weather);

        Assert.True(success);
        Assert.Equal("daily", parameters!.Value.GetProperty("forecastType").GetString());
    }

    [Fact]
    public async Task ExtractWeatherParameters_15Day_ReturnsFifteenDayForecast()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Give me the 15-day forecast for Dallas", Intent.Weather);

        Assert.True(success);
        Assert.Equal("15-day", parameters!.Value.GetProperty("forecastType").GetString());
    }

    [Fact]
    public async Task ExtractWeatherParameters_Hourly_ReturnsHourlyForecastType()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Give me the hourly forecast for Dallas", Intent.Weather);

        Assert.True(success);
        Assert.Equal("hourly", parameters!.Value.GetProperty("forecastType").GetString());
    }

    [Fact]
    public async Task ExtractWeatherParameters_WithFreeTextLocation_ResolvesViaGeocoder()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("What's the weather in Germantown MD?", Intent.Weather);

        Assert.True(success);
        Assert.Equal(39.1732, parameters!.Value.GetProperty("latitude").GetDouble(), 4);
    }

    [Fact]
    public async Task ExtractSensorParameters_WithSensorId_ReturnsSensorId()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Show diagnostics for sensor-001", Intent.Sensor);

        Assert.True(success);
        Assert.Equal("sensor-001", parameters!.Value.GetProperty("sensorId").GetString());
    }

    [Fact]
    public async Task ExtractSensorParameters_MissingSensorId_ReturnsError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("Show me sensor diagnostics", Intent.Sensor);

        Assert.False(success);
        Assert.Contains("sensor_id", error);
    }

    [Fact]
    public async Task ExtractInformerParameters_WithZone_ReturnsZone()
    {
        var (success, parameters, _) = await _extractor.ExtractParameters("Is the horn in zone-a powered on?", Intent.Informer);

        Assert.True(success);
        Assert.Equal("zone-a", parameters!.Value.GetProperty("zone").GetString());
    }

    [Fact]
    public async Task ExtractInformerParameters_MissingBoth_ReturnsError()
    {
        var (success, parameters, error) = await _extractor.ExtractParameters("What is the device status?", Intent.Informer);

        Assert.False(success);
        Assert.Contains("informer_id", error);
    }
}
