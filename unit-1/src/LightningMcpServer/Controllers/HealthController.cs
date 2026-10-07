using Microsoft.AspNetCore.Mvc;

namespace LightningMcpServer.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        _logger.LogInformation("Health check requested");
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow.ToString("O") });
    }
}
