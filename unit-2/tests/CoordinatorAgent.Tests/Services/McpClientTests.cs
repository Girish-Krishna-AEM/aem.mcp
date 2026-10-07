using System.Net;
using System.Text;
using System.Text.Json;
using CoordinatorAgent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoordinatorAgent.Tests.Services;

public class McpClientTests
{
    private static JsonElement EmptyArguments => JsonDocument.Parse("{}").RootElement;

    [Fact]
    public async Task CallToolAsync_Success_ReturnsResult()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result":{"strikeCount":3}}""", Encoding.UTF8, "application/json")
        });

        var result = await client.CallToolAsync("get_lightning_strikes_near_location", EmptyArguments);

        Assert.Equal(3, result.GetProperty("strikeCount").GetInt32());
    }

    [Fact]
    public async Task CallToolAsync_BadRequest_ThrowsWithStatusCode400()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"Invalid input"}""", Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<McpServerException>(() => client.CallToolAsync("get_sensor_diagnostics", EmptyArguments));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("Invalid input", ex.Message);
    }

    [Fact]
    public async Task CallToolAsync_ServerError_ThrowsWithStatusCode500()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":"boom"}""", Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<McpServerException>(() => client.CallToolAsync("get_sensor_diagnostics", EmptyArguments));
        Assert.Equal(500, ex.StatusCode);
    }

    [Fact]
    public async Task CallToolAsync_NetworkFailure_ThrowsHttpRequestException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CallToolAsync("get_sensor_diagnostics", EmptyArguments));
    }

    [Fact]
    public async Task IsHealthyAsync_Success_ReturnsTrue()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var healthy = await client.IsHealthyAsync();

        Assert.True(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_Exception_ReturnsFalse()
    {
        var client = CreateClient(_ => throw new HttpRequestException("unreachable"));

        var healthy = await client.IsHealthyAsync();

        Assert.False(healthy);
    }

    private static McpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://lightning-mcp-server:8000") };
        var factory = new SingleClientHttpClientFactory(httpClient);
        return new McpClient(factory, NullLogger<McpClient>.Instance);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private class SingleClientHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }
}
