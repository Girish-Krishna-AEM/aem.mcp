using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LightningMcpServer.ExternalApis;
using ModelContextProtocol.Client;

namespace LightningMcpServer.Tests.Integration;

public class McpSdkIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _httpClient;
    private McpClient? _mcpClient;

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
        _httpClient = _factory.CreateClient();
        await Task.Delay(500); // Allow server startup

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(_httpClient.BaseAddress!, "/mcp") },
            _httpClient,
            loggerFactory: null,
            ownsHttpClient: false);

        _mcpClient = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync()
    {
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
        }
        _httpClient?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task Initialize_HandshakeSucceeds()
    {
        // A successful InitializeAsync() in InitializeAsync() above proves the
        // initialize/capabilities handshake completed; this assertion just
        // confirms the client object is usable afterward.
        Assert.NotNull(_mcpClient);
    }

    [Fact]
    public async Task ListTools_ReturnsAllFourTools()
    {
        var tools = await _mcpClient!.ListToolsAsync();

        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(4, names.Count);
        Assert.Contains("get_lightning_strikes_near_location", names);
        Assert.Contains("get_weather_forecast", names);
        Assert.Contains("get_sensor_diagnostics", names);
        Assert.Contains("get_informer_status", names);
    }

    [Fact]
    public async Task CallTool_GetLightningStrikesNearLocation_ReturnsResult()
    {
        var result = await _mcpClient!.CallToolAsync(
            "get_lightning_strikes_near_location",
            new Dictionary<string, object?>
            {
                ["latitude"] = 30.2672,
                ["longitude"] = -97.7431,
                ["radius"] = 50.0,
                ["radiusUnit"] = "km"
            });

        Assert.NotEqual(true, result.IsError);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task CallTool_WithoutIncludeDetails_OmitsStrikesArrayButKeepsSummaryCounts()
    {
        var result = await _mcpClient!.CallToolAsync(
            "get_lightning_strikes_near_location",
            new Dictionary<string, object?>
            {
                ["latitude"] = 30.2672,
                ["longitude"] = -97.7431,
                ["radius"] = 50.0,
                ["radiusUnit"] = "km"
            });

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text;
        using var json = System.Text.Json.JsonDocument.Parse(text);

        Assert.Equal(1, json.RootElement.GetProperty("strikeCount").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("cloudToGroundCount").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("intraCloudCount").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("strikes").GetArrayLength());
    }

    [Fact]
    public async Task CallTool_WithIncludeDetailsTrue_ReturnsPopulatedStrikesArray()
    {
        var result = await _mcpClient!.CallToolAsync(
            "get_lightning_strikes_near_location",
            new Dictionary<string, object?>
            {
                ["latitude"] = 30.2672,
                ["longitude"] = -97.7431,
                ["radius"] = 50.0,
                ["radiusUnit"] = "km",
                ["includeDetails"] = true
            });

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text;
        using var json = System.Text.Json.JsonDocument.Parse(text);

        var strikes = json.RootElement.GetProperty("strikes");
        Assert.Equal(1, strikes.GetArrayLength());
        Assert.Equal("cg", strikes[0].GetProperty("type").GetString());
    }
}
