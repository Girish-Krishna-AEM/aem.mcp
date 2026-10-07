using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoordinatorAgent.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoordinatorAgent.Tests.Integration;

public class CoordinatorIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private FakeMcpHandler? _fakeMcpHandler;
    private FakeGeocoderHandler? _fakeGeocoderHandler;

    public Task InitializeAsync()
    {
        _fakeMcpHandler = new FakeMcpHandler();
        _fakeGeocoderHandler = new FakeGeocoderHandler();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddHttpClient(McpClient.HttpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() => _fakeMcpHandler!);

                    services.AddHttpClient<ILocationGeocoder, LocationGeocoder>()
                        .ConfigurePrimaryHttpMessageHandler(() => _fakeGeocoderHandler!);

                    // Not expected to be called in these tests (the fake Census response above always resolves),
                    // but stubbed to guarantee no real network call is ever made to Nominatim from this test suite.
                    services.AddHttpClient(LocationGeocoder.NominatimHttpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() => new FakeNominatimNoMatchHandler());
                });
            });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Query_StrikeIntent_ReturnsFormattedResult()
    {
        _fakeMcpHandler!.NextResponse = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { result = new { strikeCount = 5, nearestStrikeDistance = 2.5 } })
        };

        var response = await _client!.PostAsJsonAsync("/query", new { query = "Is there lightning near Austin, TX?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("get_lightning_strikes_near_location", body.GetProperty("toolName").GetString());
        Assert.Contains("5", body.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Query_MissingLocation_ReturnsBadRequest()
    {
        var response = await _client!.PostAsJsonAsync("/query", new { query = "Is there lightning?" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("location", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Query_UnclassifiableQuery_ReturnsBadRequest()
    {
        var response = await _client!.PostAsJsonAsync("/query", new { query = "Hello there" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Query_McpServerUnreachable_ReturnsServiceUnavailable()
    {
        _fakeMcpHandler!.NextResponse = _ => throw new HttpRequestException("connection refused");

        var response = await _client!.PostAsJsonAsync("/query", new { query = "Show diagnostics for sensor-001" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsStatusAndMcpConnectivity()
    {
        _fakeMcpHandler!.NextResponse = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var response = await _client!.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("reachable", body.GetProperty("mcp_server").GetString());
    }

    private class FakeMcpHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? NextResponse { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (NextResponse is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            return Task.FromResult(NextResponse(request));
        }
    }

    private class FakeGeocoderHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    result = new
                    {
                        addressMatches = new[]
                        {
                            new { matchedAddress = "Austin, TX, 78701", coordinates = new { x = -97.7431, y = 30.2672 } }
                        }
                    }
                })
            };

            return Task.FromResult(response);
        }
    }

    private class FakeNominatimNoMatchHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            });
        }
    }
}
