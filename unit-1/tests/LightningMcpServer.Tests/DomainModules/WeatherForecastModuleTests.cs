using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningCommon;

namespace LightningMcpServer.Tests.DomainModules;

public class WeatherForecastModuleTests
{
    private readonly IWeatherForecastModule _module;

    public WeatherForecastModuleTests()
    {
        var loggerMock = new Mock<ILogger<WeatherForecastModule>>();
        _module = new WeatherForecastModule(loggerMock.Object);
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
}
