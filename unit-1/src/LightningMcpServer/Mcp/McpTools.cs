using System.ComponentModel;
using LightningCommon;
using LightningMcpServer.DomainModules;
using ModelContextProtocol.Server;

namespace LightningMcpServer.Mcp;

[McpServerToolType]
public static class McpTools
{
    [McpServerTool(Name = "get_lightning_strikes_near_location"), Description("Get lightning strikes near a specific location")]
    public static GetLightningStrikesNearLocationResponse GetLightningStrikesNearLocation(
        IStrikeDetectionModule strikeModule,
        [Description("Latitude of the location")] double latitude,
        [Description("Longitude of the location")] double longitude,
        [Description("Search radius")] double radius,
        [Description("Unit of radius (km or miles)")] string radiusUnit = "km",
        [Description("Start of the pulse search window (ISO 8601). Must be before endDateTime. Defaults to endDateTime minus 5 minutes.")] DateTime? startDateTime = null,
        [Description("End of the pulse search window (ISO 8601). Defaults to current UTC time.")] DateTime? endDateTime = null,
        [Description("Pulse type filter: CG/CloudToGround or IC/IntraCloud. Omit for all types.")] string? pulseType = null,
        [Description("Include the detailed per-strike list in the response. Defaults to false — only summary counts (total/CG/IC/max intensity) are returned.")] bool includeDetails = false)
    {
        return strikeModule.GetLightningStrikesNearLocation(new GetLightningStrikesNearLocationRequest
        {
            Latitude = latitude,
            Longitude = longitude,
            Radius = radius,
            RadiusUnit = radiusUnit,
            StartDateTime = startDateTime,
            EndDateTime = endDateTime,
            PulseType = pulseType,
            IncludeDetails = includeDetails
        });
    }

    [McpServerTool(Name = "get_weather_forecast"), Description("Get weather forecast for a location")]
    public static GetWeatherForecastResponse GetWeatherForecast(
        IWeatherForecastModule weatherModule,
        [Description("Latitude of the location")] double latitude,
        [Description("Longitude of the location")] double longitude,
        [Description("Type of forecast (daily or 15-day)")] string forecastType = "daily")
    {
        return weatherModule.GetWeatherForecast(new GetWeatherForecastRequest
        {
            Latitude = latitude,
            Longitude = longitude,
            ForecastType = forecastType
        });
    }

    [McpServerTool(Name = "get_sensor_diagnostics"), Description("Get diagnostics for a lightning detection sensor")]
    public static SensorDiagnostics GetSensorDiagnostics(
        ISensorDiagnosticsModule sensorModule,
        [Description("ID of the sensor")] string sensorId)
    {
        return sensorModule.GetSensorDiagnostics(new GetSensorDiagnosticsRequest
        {
            SensorId = sensorId
        });
    }

    [McpServerTool(Name = "get_informer_status"), Description("Get status of a warning informer device (strobe, horn, etc.)")]
    public static InformerStatus GetInformerStatus(
        IInformerStatusModule informerModule,
        [Description("ID of the informer device")] string? informerId = null,
        [Description("Zone identifier")] string? zone = null)
    {
        return informerModule.GetInformerStatus(new GetInformerStatusRequest
        {
            InformerId = informerId,
            Zone = zone
        });
    }
}
