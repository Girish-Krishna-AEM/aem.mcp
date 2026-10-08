using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LightningMcpServer.ExternalApis;
using System.Text.Json;
using System.Net;

namespace LightningMcpServer.Tests.Integration;

public class McpServerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ILightningPulseApiClient>();
                    services.AddSingleton<ILightningPulseApiClient>(new FakeLightningPulseApiClient());
                });
            });
        _client = _factory.CreateClient();
        await Task.Delay(500); // Allow server startup
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client!.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", content);
    }

    [Fact]
    public async Task ListTools_ReturnsAllTools()
    {
        var payload = new
        {
            method = "tools/list"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.True(json.RootElement.TryGetProperty("tools", out var tools));
        var toolArray = tools.EnumerateArray().ToList();
        Assert.Equal(6, toolArray.Count);
    }

    [Fact]
    public async Task CallStrikeTool_WithValidInput_ReturnsStrikes()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "get_lightning_strikes_near_location",
                arguments = new
                {
                    latitude = 30.2672,
                    longitude = -97.7431,
                    radius = 50,
                    radiusUnit = "km"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("strikeCount", content);
    }

    [Fact]
    public async Task CallStrikeTool_WithoutIncludeDetails_OmitsStrikesArray()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "get_lightning_strikes_near_location",
                arguments = new
                {
                    latitude = 30.2672,
                    longitude = -97.7431,
                    radius = 50,
                    radiusUnit = "km"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var result = json.RootElement.GetProperty("result");
        Assert.Equal(1, result.GetProperty("strikeCount").GetInt32());
        Assert.Equal(0, result.GetProperty("strikes").GetArrayLength());
    }

    [Fact]
    public async Task CallStrikeTool_WithIncludeDetailsTrue_ReturnsPopulatedStrikesArray()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "get_lightning_strikes_near_location",
                arguments = new
                {
                    latitude = 30.2672,
                    longitude = -97.7431,
                    radius = 50,
                    radiusUnit = "km",
                    includeDetails = true
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var strikes = json.RootElement.GetProperty("result").GetProperty("strikes");
        Assert.Equal(1, strikes.GetArrayLength());
        Assert.Equal("cg", strikes[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task CallWeatherTool_WithValidInput_ReturnsForecast()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "get_weather_forecast",
                arguments = new
                {
                    latitude = 30.2672,
                    longitude = -97.7431,
                    forecastType = "daily"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("forecast", content);
    }

    [Fact]
    public async Task CallSensorTool_WithValidInput_ReturnsDiagnostics()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "get_sensor_diagnostics",
                arguments = new
                {
                    sensorId = "sensor-001"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("detectionEfficiency", content);
    }

    [Fact]
    public async Task CallInformerTool_WithValidInput_ReturnsStatus()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "get_informer_status",
                arguments = new
                {
                    informerId = "informer-001"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", content);
    }

    [Fact]
    public async Task UnknownTool_ReturnsErrorResponse()
    {
        var payload = new
        {
            method = "tools/call",
            @params = new
            {
                name = "unknown_tool",
                arguments = new { }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
