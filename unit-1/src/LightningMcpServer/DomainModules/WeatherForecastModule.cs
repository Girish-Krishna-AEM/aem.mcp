using LightningCommon;
using System;
using System.Collections.Generic;

namespace LightningMcpServer.DomainModules;

public class WeatherForecastModule : IWeatherForecastModule
{
    private readonly ILogger<WeatherForecastModule> _logger;
    private readonly string[] _conditions = { "Sunny", "Cloudy", "Rainy", "Stormy", "Partly Cloudy" };

    public WeatherForecastModule(ILogger<WeatherForecastModule> logger)
    {
        _logger = logger;
    }

    public GetWeatherForecastResponse GetWeatherForecast(GetWeatherForecastRequest request)
    {
        _logger.LogInformation(
            "Weather forecast requested: location=({Latitude},{Longitude}), type={ForecastType}",
            request.Latitude, request.Longitude, request.ForecastType);

        ValidateRequest(request);

        var seed = GenerateSeed(request.Latitude, request.Longitude);
        var random = new Random(seed);

        var dayCount = request.ForecastType == "daily" ? 1 : 15;
        var forecast = new List<WeatherForecastEntry>();

        for (int i = 0; i < dayCount; i++)
        {
            var date = DateTime.UtcNow.AddDays(i);
            forecast.Add(new WeatherForecastEntry
            {
                Date = date.ToString("yyyy-MM-dd"),
                Condition = _conditions[random.Next(_conditions.Length)],
                Temp = 15 + random.Next(0, 30),
                PrecipitationPercent = random.Next(0, 101),
                LightningRisk = random.Next(0, 101)
            });
        }

        _logger.LogInformation(
            "Weather forecast result: entries={EntryCount}, type={ForecastType}",
            forecast.Count, request.ForecastType);

        return new GetWeatherForecastResponse { Forecast = forecast };
    }

    private void ValidateRequest(GetWeatherForecastRequest request)
    {
        if (double.IsNaN(request.Latitude) || double.IsNaN(request.Longitude))
            throw new ArgumentException("Invalid location coordinates");

        if (!new[] { "daily", "15-day" }.Contains(request.ForecastType))
            throw new ArgumentException("ForecastType must be 'daily' or '15-day'");
    }

    private int GenerateSeed(double latitude, double longitude)
    {
        var combined = $"{latitude:F6}{longitude:F6}";
        return combined.GetHashCode();
    }
}
