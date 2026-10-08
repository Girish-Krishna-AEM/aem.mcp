using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface IWeatherForecastModule
{
    GetWeatherForecastResponse GetWeatherForecast(GetWeatherForecastRequest request);

    GetDailyWeatherForecastResponse GetDailyWeatherForecast(GetDailyWeatherForecastRequest request);

    GetHourlyWeatherForecastResponse GetHourlyWeatherForecast(GetHourlyWeatherForecastRequest request);
}
