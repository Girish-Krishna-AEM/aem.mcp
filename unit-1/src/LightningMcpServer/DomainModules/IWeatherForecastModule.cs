using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface IWeatherForecastModule
{
    GetWeatherForecastResponse GetWeatherForecast(GetWeatherForecastRequest request);
}
