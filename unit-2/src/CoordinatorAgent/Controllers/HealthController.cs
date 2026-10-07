using CoordinatorAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoordinatorAgent.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly IMcpClient _mcpClient;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IMcpClient mcpClient, ILogger<HealthController> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health check requested");
        var mcpHealthy = await _mcpClient.IsHealthyAsync(cancellationToken);

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow.ToString("O"),
            mcp_server = mcpHealthy ? "reachable" : "unreachable"
        });
    }
}
