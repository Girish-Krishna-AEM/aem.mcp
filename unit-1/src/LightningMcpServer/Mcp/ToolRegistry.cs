using LightningMcpServer.DomainModules;
using LightningCommon;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightningMcpServer.Mcp;

public class ToolRegistry : IToolRegistry
{
    private readonly IStrikeDetectionModule _strikeModule;
    private readonly IWeatherForecastModule _weatherModule;
    private readonly ISensorDiagnosticsModule _sensorModule;
    private readonly IInformerStatusModule _informerModule;
    private readonly ILogger<ToolRegistry> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ToolRegistry(
        IStrikeDetectionModule strikeModule,
        IWeatherForecastModule weatherModule,
        ISensorDiagnosticsModule sensorModule,
        IInformerStatusModule informerModule,
        ILogger<ToolRegistry> logger)
    {
        _strikeModule = strikeModule;
        _weatherModule = weatherModule;
        _sensorModule = sensorModule;
        _informerModule = informerModule;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public JsonElement ListTools()
    {
        _logger.LogInformation("Listing all tools");

        var tools = new object[]
        {
            new
            {
                name = "get_lightning_strikes_near_location",
                description = "Get lightning strikes near a specific location",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        latitude = new { type = "number", description = "Latitude of the location" },
                        longitude = new { type = "number", description = "Longitude of the location" },
                        radius = new { type = "number", description = "Search radius" },
                        radiusUnit = new { type = "string", description = "Unit of radius (km or miles)" },
                        startDateTime = new { type = "string", description = "Start of the pulse search window (ISO 8601). Must be before endDateTime. Defaults to endDateTime minus 5 minutes." },
                        endDateTime = new { type = "string", description = "End of the pulse search window (ISO 8601). Defaults to current UTC time." },
                        pulseType = new { type = "string", description = "Pulse type filter: CG/CloudToGround or IC/IntraCloud. Omit for all types." },
                        includeDetails = new { type = "boolean", description = "Include the detailed per-strike list in the response. Defaults to false — only summary counts (total/CG/IC/max intensity) are returned." }
                    },
                    required = new[] { "latitude", "longitude", "radius" }
                }
            },
            new
            {
                name = "get_weather_forecast",
                description = "Get weather forecast for a location",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        latitude = new { type = "number", description = "Latitude of the location" },
                        longitude = new { type = "number", description = "Longitude of the location" },
                        forecastType = new { type = "string", description = "Type of forecast (daily or 15-day)" }
                    },
                    required = new[] { "latitude", "longitude" }
                }
            },
            new
            {
                name = "get_sensor_diagnostics",
                description = "Get diagnostics for a lightning detection sensor",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sensorId = new { type = "string", description = "ID of the sensor" }
                    },
                    required = new[] { "sensorId" }
                }
            },
            new
            {
                name = "get_informer_status",
                description = "Get status of a warning informer device (strobe, horn, etc.)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        informerId = new { type = "string", description = "ID of the informer device" },
                        zone = new { type = "string", description = "Zone identifier" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(new { tools }, _jsonOptions);
        return JsonDocument.Parse(json).RootElement;
    }

    public JsonElement CallTool(string toolName, JsonElement arguments)
    {
        _logger.LogInformation("Calling tool: {ToolName}", toolName);

        try
        {
            var result = toolName switch
            {
                "get_lightning_strikes_near_location" => HandleStrikeDetection(arguments),
                "get_weather_forecast" => HandleWeatherForecast(arguments),
                "get_sensor_diagnostics" => HandleSensorDiagnostics(arguments),
                "get_informer_status" => HandleInformerStatus(arguments),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };

            _logger.LogInformation("Tool {ToolName} executed successfully", toolName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            throw;
        }
    }

    private JsonElement HandleStrikeDetection(JsonElement arguments)
    {
        var request = new GetLightningStrikesNearLocationRequest
        {
            Latitude = arguments.GetProperty("latitude").GetDouble(),
            Longitude = arguments.GetProperty("longitude").GetDouble(),
            Radius = arguments.GetProperty("radius").GetDouble(),
            RadiusUnit = arguments.TryGetProperty("radiusUnit", out var ru) ? ru.GetString() ?? "km" : "km",
            StartDateTime = arguments.TryGetProperty("startDateTime", out var sdt) ? sdt.GetDateTime() : null,
            EndDateTime = arguments.TryGetProperty("endDateTime", out var edt) ? edt.GetDateTime() : null,
            PulseType = arguments.TryGetProperty("pulseType", out var pt) ? pt.GetString() : null,
            IncludeDetails = arguments.TryGetProperty("includeDetails", out var id) && id.GetBoolean()
        };

        var response = _strikeModule.GetLightningStrikesNearLocation(request);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return JsonDocument.Parse(json).RootElement;
    }

    private JsonElement HandleWeatherForecast(JsonElement arguments)
    {
        var request = new GetWeatherForecastRequest
        {
            Latitude = arguments.GetProperty("latitude").GetDouble(),
            Longitude = arguments.GetProperty("longitude").GetDouble(),
            ForecastType = arguments.TryGetProperty("forecastType", out var ft) ? ft.GetString() ?? "daily" : "daily"
        };

        var response = _weatherModule.GetWeatherForecast(request);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return JsonDocument.Parse(json).RootElement;
    }

    private JsonElement HandleSensorDiagnostics(JsonElement arguments)
    {
        var request = new GetSensorDiagnosticsRequest
        {
            SensorId = arguments.GetProperty("sensorId").GetString() ?? string.Empty
        };

        var response = _sensorModule.GetSensorDiagnostics(request);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return JsonDocument.Parse(json).RootElement;
    }

    private JsonElement HandleInformerStatus(JsonElement arguments)
    {
        var request = new GetInformerStatusRequest
        {
            InformerId = arguments.TryGetProperty("informerId", out var id) ? id.GetString() : null,
            Zone = arguments.TryGetProperty("zone", out var z) ? z.GetString() : null
        };

        var response = _informerModule.GetInformerStatus(request);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return JsonDocument.Parse(json).RootElement;
    }
}
