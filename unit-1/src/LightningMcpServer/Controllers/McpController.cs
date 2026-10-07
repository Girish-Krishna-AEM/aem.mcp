using LightningMcpServer.Mcp;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LightningMcpServer.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<McpController> _logger;

    public McpController(IToolRegistry toolRegistry, ILogger<McpController> logger)
    {
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    [HttpPost("messages")]
    public IActionResult HandleMcpMessage([FromBody] JsonElement request)
    {
        _logger.LogInformation("MCP message received");

        try
        {
            if (!request.TryGetProperty("method", out var methodEl))
            {
                return BadRequest(new { error = "Missing 'method' field" });
            }

            var method = methodEl.GetString();

            return method switch
            {
                "tools/list" => Ok(_toolRegistry.ListTools()),
                "tools/call" => HandleToolCall(request),
                _ => BadRequest(new { error = $"Unknown method: {method}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP message");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private IActionResult HandleToolCall(JsonElement request)
    {
        try
        {
            if (!request.TryGetProperty("params", out var paramsEl))
            {
                return BadRequest(new { error = "Missing 'params' field for tools/call" });
            }

            if (!paramsEl.TryGetProperty("name", out var toolNameEl))
            {
                return BadRequest(new { error = "Missing tool 'name' in params" });
            }

            var toolName = toolNameEl.GetString();
            if (string.IsNullOrEmpty(toolName))
            {
                return BadRequest(new { error = "Tool name cannot be empty" });
            }

            var arguments = paramsEl.TryGetProperty("arguments", out var args) ? args : JsonDocument.Parse("{}").RootElement;

            _logger.LogInformation("Calling tool: {ToolName}", toolName);
            var result = _toolRegistry.CallTool(toolName, arguments);

            return Ok(new { result });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid tool call");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling tool");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
