using LightningCommon;
using LightningMcpServer.ExternalApis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LightningMcpServer.DomainModules;

public class WeatherForecastModule : IWeatherForecastModule
{
    private readonly ILogger<WeatherForecastModule> _logger;
    private readonly IWeatherForecastApiClient _weatherApiClient;
    private readonly string[] _conditions = { "Sunny", "Cloudy", "Rainy", "Stormy", "Partly Cloudy" };

    public WeatherForecastModule(ILogger<WeatherForecastModule> logger, IWeatherForecastApiClient weatherApiClient)
    {
        _logger = logger;
        _weatherApiClient = weatherApiClient;
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

    public GetDailyWeatherForecastResponse GetDailyWeatherForecast(GetDailyWeatherForecastRequest request)
    {
        _logger.LogInformation(
            "Daily weather forecast requested: zipCode={ZipCode}, latitude={Latitude}, longitude={Longitude}",
            request.ZipCode, request.Latitude, request.Longitude);

        ValidateDailyRequest(request);

        var apiResponse = string.IsNullOrWhiteSpace(request.ZipCode)
            ? _weatherApiClient.GetDailyForecastByLatLonAsync(request.Latitude!.Value, request.Longitude!.Value).GetAwaiter().GetResult()
            : _weatherApiClient.GetDailyForecastByZipCodeAsync(request.ZipCode).GetAwaiter().GetResult();

        var result = apiResponse.Result ?? new DailyForecastApiResult();
        var periods = result.DailyForecastPeriods.Select(MapDailyPeriod).ToList();

        _logger.LogInformation("Daily weather forecast result: periods={PeriodCount}", periods.Count);

        return new GetDailyWeatherForecastResponse
        {
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            Periods = periods
        };
    }

    public GetHourlyWeatherForecastResponse GetHourlyWeatherForecast(GetHourlyWeatherForecastRequest request)
    {
        _logger.LogInformation(
            "Hourly weather forecast requested: latitude={Latitude}, longitude={Longitude}, searchString={SearchString}",
            request.Latitude, request.Longitude, request.SearchString);

        ValidateHourlyRequest(request);

        var apiResponse = request.Latitude.HasValue && request.Longitude.HasValue
            ? _weatherApiClient.GetHourlyForecastByLatLonAsync(request.Latitude.Value, request.Longitude.Value).GetAwaiter().GetResult()
            : _weatherApiClient.GetHourlyForecastBySearchAsync(request.SearchString!).GetAwaiter().GetResult();

        var result = apiResponse.Result ?? new HourlyForecastApiResult();
        var periods = result.HourlyForecastPeriods.Select(MapHourlyPeriod).ToList();

        _logger.LogInformation("Hourly weather forecast result: periods={PeriodCount}", periods.Count);

        return new GetHourlyWeatherForecastResponse
        {
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            Periods = periods
        };
    }

    private static DailyForecastPeriod MapDailyPeriod(DailyForecastPeriodDto dto) => new()
    {
        DateUtc = dto.ForecastDateUtcStr ?? string.Empty,
        Night = dto.IsNightTimePeriod,
        CloudPct = dto.CloudCoverPercent,
        DewPointC = dto.DewPointC,
        Humidity = dto.RelativeHumidity,
        TempC = dto.TemperatureC,
        PrecipCode = dto.PrecipCode,
        PrecipPct = dto.PrecipProbability,
        StormPct = dto.ThunderstormProbability,
        WindDir = dto.WindDirectionDegrees,
        WindSpeedMs = dto.WindSpeedMetersPerSecond,
        SnowMm = dto.SnowAmountMm
    };

    private static HourlyForecastPeriod MapHourlyPeriod(HourlyForecastPeriodDto dto) => new()
    {
        DateUtc = dto.ForecastDateUtcStr ?? string.Empty,
        CloudPct = dto.CloudCoverPercent,
        DewPointC = dto.DewPointC,
        Humidity = dto.RelativeHumidity,
        TempC = dto.TemperatureC,
        IconCode = dto.IconCode,
        PrecipCode = dto.PrecipCode,
        PrecipPct = dto.PrecipProbability,
        PrecipRateMm = dto.PrecipRateMillimeters,
        StormPct = dto.ThunderstormProbability,
        WindDir = dto.WindDirectionDegrees,
        WindSpeedMs = dto.WindSpeedMetersPerSecond,
        AdjPrecipPct = dto.AdjustedPrecipProbability,
        SolarIrr = dto.SolarIrradiance,
        Pressure = dto.SurfacePressure,
        SnowRate = dto.SnowRate,
        WetBulbGlobeC = dto.WetBulbGlobeTemperatureC,
        WetBulbC = dto.WetBulbTemperatureC,
        WindGustMs = dto.WindGustMps,
        HeatIndexC = dto.HeatIndexC,
        WindChillC = dto.WindChillC
    };

    private static void ValidateDailyRequest(GetDailyWeatherForecastRequest request)
    {
        var hasZip = !string.IsNullOrWhiteSpace(request.ZipCode);
        var hasLatLon = request.Latitude.HasValue && request.Longitude.HasValue;

        if (!hasZip && !hasLatLon)
        {
            throw new ArgumentException("Provide either zipCode or both latitude and longitude.");
        }
    }

    private static void ValidateHourlyRequest(GetHourlyWeatherForecastRequest request)
    {
        var hasLatLon = request.Latitude.HasValue && request.Longitude.HasValue;
        var hasSearch = !string.IsNullOrWhiteSpace(request.SearchString);

        if (!hasLatLon && !hasSearch)
        {
            throw new ArgumentException("Provide either latitude/longitude or searchString.");
        }
    }
}
