using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningMcpServer.ExternalApis;
using LightningCommon;

namespace LightningMcpServer.Tests.DomainModules;

public class WeatherForecastModuleTests
{
    private readonly Mock<IWeatherForecastApiClient> _apiClientMock;
    private readonly IWeatherForecastModule _module;

    public WeatherForecastModuleTests()
    {
        var loggerMock = new Mock<ILogger<WeatherForecastModule>>();
        _apiClientMock = new Mock<IWeatherForecastApiClient>();
        _module = new WeatherForecastModule(loggerMock.Object, _apiClientMock.Object);
    }

    [Fact]
    public void GetWeatherForecast_DailyForecast_ReturnsOneEntry()
    {
        var request = new GetWeatherForecastRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            ForecastType = "daily"
        };

        var response = _module.GetWeatherForecast(request);

        Assert.NotNull(response);
        Assert.Single(response.Forecast);
    }

    [Fact]
    public void GetWeatherForecast_15DayForecast_Returns15Entries()
    {
        var request = new GetWeatherForecastRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            ForecastType = "15-day"
        };

        var response = _module.GetWeatherForecast(request);

        Assert.NotNull(response);
        Assert.Equal(15, response.Forecast.Count);
    }

    [Fact]
    public void GetWeatherForecast_WithInvalidForecastType_ThrowsArgumentException()
    {
        var request = new GetWeatherForecastRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            ForecastType = "7-day"
        };

        Assert.Throws<ArgumentException>(() => _module.GetWeatherForecast(request));
    }

    [Fact]
    public void GetWeatherForecast_DeterministicResults()
    {
        var request1 = new GetWeatherForecastRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            ForecastType = "15-day"
        };

        var request2 = new GetWeatherForecastRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431,
            ForecastType = "15-day"
        };

        var response1 = _module.GetWeatherForecast(request1);
        var response2 = _module.GetWeatherForecast(request2);

        Assert.Equal(response1.Forecast.Count, response2.Forecast.Count);
        for (int i = 0; i < response1.Forecast.Count; i++)
        {
            Assert.Equal(response1.Forecast[i].Date, response2.Forecast[i].Date);
            Assert.Equal(response1.Forecast[i].Condition, response2.Forecast[i].Condition);
            Assert.Equal(response1.Forecast[i].Temp, response2.Forecast[i].Temp);
        }
    }

    [Fact]
    public void GetDailyWeatherForecast_ByZipCode_MapsTrimmedAbbreviatedFields()
    {
        _apiClientMock
            .Setup(c => c.GetDailyForecastByZipCodeAsync("20874", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyForecastApiResponse
            {
                Result = new DailyForecastApiResult
                {
                    Latitude = 39.1732,
                    Longitude = -77.2719,
                    DailyForecastPeriods = new List<DailyForecastPeriodDto>
                    {
                        new()
                        {
                            ForecastDateUtcStr = "2026-10-07T00:00:00.0000000Z",
                            IsNightTimePeriod = false,
                            CloudCoverPercent = 0,
                            DewPointC = 11.2,
                            RelativeHumidity = 25,
                            TemperatureC = 36.1,
                            PrecipCode = 0,
                            PrecipProbability = 0,
                            ThunderstormProbability = 0,
                            WindDirectionDegrees = 315,
                            WindSpeedMetersPerSecond = 1.5,
                            SnowAmountMm = 0.0
                        }
                    }
                }
            });

        var response = _module.GetDailyWeatherForecast(new GetDailyWeatherForecastRequest { ZipCode = "20874" });

        Assert.Equal(39.1732, response.Latitude);
        Assert.Equal(-77.2719, response.Longitude);
        var period = Assert.Single(response.Periods);
        Assert.Equal("2026-10-07T00:00:00.0000000Z", period.DateUtc);
        Assert.False(period.Night);
        Assert.Equal(11.2, period.DewPointC);
        Assert.Equal(25, period.Humidity);
        Assert.Equal(36.1, period.TempC);
        Assert.Equal(315, period.WindDir);
        Assert.Equal(1.5, period.WindSpeedMs);
        Assert.Equal(0.0, period.SnowMm);

        _apiClientMock.Verify(c => c.GetDailyForecastByZipCodeAsync("20874", It.IsAny<CancellationToken>()), Times.Once);
        _apiClientMock.Verify(c => c.GetDailyForecastByLatLonAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetDailyWeatherForecast_ByLatLon_CallsLatLonVariant()
    {
        _apiClientMock
            .Setup(c => c.GetDailyForecastByLatLonAsync(30.2672, -97.7431, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyForecastApiResponse
            {
                Result = new DailyForecastApiResult { Latitude = 30.2672, Longitude = -97.7431 }
            });

        var response = _module.GetDailyWeatherForecast(new GetDailyWeatherForecastRequest
        {
            Latitude = 30.2672,
            Longitude = -97.7431
        });

        Assert.Equal(30.2672, response.Latitude);
        Assert.Empty(response.Periods);
        _apiClientMock.Verify(c => c.GetDailyForecastByLatLonAsync(30.2672, -97.7431, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetDailyWeatherForecast_WithNeitherZipNorLatLon_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _module.GetDailyWeatherForecast(new GetDailyWeatherForecastRequest()));
    }

    [Fact]
    public void GetHourlyWeatherForecast_ByLatLon_MapsAllNonDescriptionFields()
    {
        _apiClientMock
            .Setup(c => c.GetHourlyForecastByLatLonAsync(39.3259, -77.3514, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HourlyForecastApiResponse
            {
                Result = new HourlyForecastApiResult
                {
                    Latitude = 39.3259,
                    Longitude = -77.3514,
                    HourlyForecastPeriods = new List<HourlyForecastPeriodDto>
                    {
                        new()
                        {
                            ForecastDateUtcStr = "2026-10-07T23:00:00.0000000Z",
                            CloudCoverPercent = 1,
                            DewPointC = 13.1,
                            RelativeHumidity = 67,
                            TemperatureC = 19.4,
                            IconCode = 31,
                            PrecipCode = 0,
                            PrecipProbability = 0,
                            PrecipRateMillimeters = 0.0,
                            ThunderstormProbability = 0,
                            WindDirectionDegrees = 176,
                            WindSpeedMetersPerSecond = 2.1,
                            AdjustedPrecipProbability = 0,
                            SolarIrradiance = 21.6,
                            SurfacePressure = 987.5,
                            SnowRate = 0.0,
                            WetBulbGlobeTemperatureC = 16.0,
                            WetBulbTemperatureC = 15.0,
                            WindGustMps = 5.8,
                            HeatIndexC = 19.0,
                            WindChillC = 19.0
                        }
                    }
                }
            });

        var response = _module.GetHourlyWeatherForecast(new GetHourlyWeatherForecastRequest
        {
            Latitude = 39.3259,
            Longitude = -77.3514
        });

        Assert.Equal(39.3259, response.Latitude);
        var period = Assert.Single(response.Periods);
        Assert.Equal("2026-10-07T23:00:00.0000000Z", period.DateUtc);
        Assert.Equal(1, period.CloudPct);
        Assert.Equal(13.1, period.DewPointC);
        Assert.Equal(67, period.Humidity);
        Assert.Equal(19.4, period.TempC);
        Assert.Equal(31, period.IconCode);
        Assert.Equal(176, period.WindDir);
        Assert.Equal(2.1, period.WindSpeedMs);
        Assert.Equal(21.6, period.SolarIrr);
        Assert.Equal(987.5, period.Pressure);
        Assert.Equal(16.0, period.WetBulbGlobeC);
        Assert.Equal(15.0, period.WetBulbC);
        Assert.Equal(5.8, period.WindGustMs);
        Assert.Equal(19.0, period.HeatIndexC);
        Assert.Equal(19.0, period.WindChillC);
    }

    [Fact]
    public void GetHourlyWeatherForecast_BySearchString_CallsSearchVariant()
    {
        _apiClientMock
            .Setup(c => c.GetHourlyForecastBySearchAsync("Urbana,MD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HourlyForecastApiResponse
            {
                Result = new HourlyForecastApiResult { Latitude = 39.3, Longitude = -77.3 }
            });

        var response = _module.GetHourlyWeatherForecast(new GetHourlyWeatherForecastRequest { SearchString = "Urbana,MD" });

        Assert.Equal(39.3, response.Latitude);
        _apiClientMock.Verify(c => c.GetHourlyForecastBySearchAsync("Urbana,MD", It.IsAny<CancellationToken>()), Times.Once);
        _apiClientMock.Verify(c => c.GetHourlyForecastByLatLonAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetHourlyWeatherForecast_WithNeitherLatLonNorSearch_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _module.GetHourlyWeatherForecast(new GetHourlyWeatherForecastRequest()));
    }
}
