using CoordinatorAgent.Models;
using CoordinatorAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoordinatorAgent.Controllers;

public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
}

[ApiController]
[Route("")]
public class QueryController : ControllerBase
{
    private static readonly Dictionary<Intent, string> ToolNames = new()
    {
        [Intent.Strike] = "get_lightning_strikes_near_location",
        [Intent.Sensor] = "get_sensor_diagnostics",
        [Intent.Informer] = "get_informer_status"
    };

    private readonly IIntentClassifier _intentClassifier;
    private readonly IParameterExtractor _parameterExtractor;
    private readonly IMcpClient _mcpClient;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IIntentClassifier intentClassifier,
        IParameterExtractor parameterExtractor,
        IMcpClient mcpClient,
        ILogger<QueryController> logger)
    {
        _intentClassifier = intentClassifier;
        _parameterExtractor = parameterExtractor;
        _mcpClient = mcpClient;
        _logger = logger;
    }

    [HttpPost("query")]
    public async Task<IActionResult> HandleQuery([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query must not be empty." });
        }

        _logger.LogInformation("Request received: {Query}", request.Query);

        var intent = _intentClassifier.Classify(request.Query);
        if (intent is null)
        {
            return BadRequest(new { error = "Unable to determine what you're asking for. Try mentioning lightning strikes, weather, sensor diagnostics, or informer status." });
        }

        var (success, parameters, error) = await _parameterExtractor.ExtractParameters(request.Query, intent.Value);
        if (!success || parameters is null)
        {
            _logger.LogWarning("Parameter extraction failed for query {Query}: {Error}", request.Query, error);
            return BadRequest(new { error });
        }

        var toolName = intent.Value == Intent.Weather
            ? ResolveWeatherToolName(parameters.Value)
            : ToolNames[intent.Value];

        _logger.LogInformation(
            "Parameters extracted for tool {ToolName} from query {Query}: {Parameters}",
            toolName, request.Query, parameters.Value.GetRawText());

        try
        {
            var result = await _mcpClient.CallToolAsync(toolName, parameters.Value, cancellationToken);
            _logger.LogInformation("Response returned successfully for tool {ToolName}: {Result}", toolName, result.GetRawText());
            return Ok(ResultFormatter.Format(toolName, intent.Value, result));
        }
        catch (McpServerException ex) when (ex.StatusCode is >= 400 and < 500)
        {
            _logger.LogWarning(ex, "MCP Server rejected tool call {ToolName}", toolName);
            return BadRequest(new { error = ex.Message });
        }
        catch (McpServerException ex)
        {
            _logger.LogError(ex, "MCP Server error for tool {ToolName}", toolName);
            return StatusCode(503, new { error = "MCP Server is currently unavailable. Please try again in a moment." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MCP Server unreachable for tool {ToolName}", toolName);
            return StatusCode(503, new { error = "MCP Server is currently unavailable. Please try again in a moment." });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "MCP Server call timed out for tool {ToolName}", toolName);
            return StatusCode(503, new { error = "MCP Server is currently unavailable. Please try again in a moment." });
        }
    }

    /// <summary>Real daily/hourly forecast tools handle the common case; "15-day" still
    /// routes to the original mock get_weather_forecast tool, which only supports that type.</summary>
    private static string ResolveWeatherToolName(System.Text.Json.JsonElement parameters)
    {
        var forecastType = parameters.TryGetProperty("forecastType", out var ft) ? ft.GetString() : "daily";
        return forecastType switch
        {
            "hourly" => "get_hourly_weather_forecast",
            "15-day" => "get_weather_forecast",
            _ => "get_daily_weather_forecast"
        };
    }
}
