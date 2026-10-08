using System.Net.Http.Json;
using System.Text.Json;

namespace CoordinatorAgent.Services;

public class McpClient : IMcpClient
{
    public const string HttpClientName = "McpServer";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<McpClient> _logger;

    public McpClient(IHttpClientFactory httpClientFactory, ILogger<McpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JsonElement> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var request = new
        {
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments
            }
        };

        var requestUrl = client.BaseAddress is not null ? new Uri(client.BaseAddress, "/mcp/messages") : new Uri("/mcp/messages", UriKind.Relative);
        _logger.LogInformation(
            "Calling MCP Server: {Method} {Url} for tool {ToolName}, parameters={Request}",
            HttpMethod.Post, requestUrl, toolName, JsonSerializer.Serialize(request));

        using var response = await client.PostAsJsonAsync("/mcp/messages", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("MCP Server returned {StatusCode} for tool {ToolName}: {Body}", (int)response.StatusCode, toolName, body);
            throw new McpServerException((int)response.StatusCode, ExtractErrorMessage(body));
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("Received MCP response for {ToolName}: {Response}", toolName, responseBody);

        var payload = JsonSerializer.Deserialize<JsonElement>(responseBody);
        return payload.TryGetProperty("result", out var result) ? result : payload;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var response = await client.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP Server health probe failed");
            return false;
        }
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? "MCP Server returned an error.";
            }
        }
        catch (JsonException)
        {
        }

        return "MCP Server returned an error.";
    }
}
