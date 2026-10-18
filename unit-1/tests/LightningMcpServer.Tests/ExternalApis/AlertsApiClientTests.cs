using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LightningMcpServer.ExternalApis;
using Xunit;

namespace LightningMcpServer.Tests.ExternalApis;

public class AlertsApiClientTests
{
    private const string SampleJson = """
        { "r": [ { "nm": "Site A", "a": [
            { "atn": "LX", "m": "test message", "actutc": "2026-10-16T19:42:41.418Z",
              "issutc": "2026-10-16T19:42:41Z", "exputc": "2026-10-16T19:44:41.295Z",
              "acutc": "2026-10-16T19:44:41.295Z", "lmutc": "2026-10-16T19:42:41.715Z",
              "uaid": "ignored", "read": false, "trig": "ignored" }
        ] } ] }
        """;

    [Fact]
    public async Task GetAlertsAsync_LiveMode_MapsAndTrimsFields()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson, System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://qa.alerts.api.enqa.co") };
        var options = Options.Create(new AlertsApiOptions { DataSource = "live" });
        var client = new AlertsApiClient(httpClient, options, NullLogger<AlertsApiClient>.Instance);

        var result = await client.GetAlertsAsync("u.100", 1792152000, 1792180200);

        Assert.Single(result);
        Assert.Equal("Site A", result[0].SiteName);
        Assert.Single(result[0].Alerts);
        Assert.Equal("LX", result[0].Alerts[0].Atn);
        Assert.Equal("test message", result[0].Alerts[0].Message);
        Assert.NotNull(result[0].Alerts[0].IssuedUtc);
    }

    [Fact]
    public async Task GetAlertsAsync_LiveMode_TimeoutThenRetrySucceeds()
    {
        var attempts = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts < 2) throw new TaskCanceledException("timeout");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "r": [] }""", System.Text.Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://qa.alerts.api.enqa.co") };
        var options = Options.Create(new AlertsApiOptions { DataSource = "live" });
        var client = new AlertsApiClient(httpClient, options, NullLogger<AlertsApiClient>.Instance);

        var result = await client.GetAlertsAsync("u.100", 0, 1);

        Assert.Empty(result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetAlertsAsync_LiveMode_ExhaustsAllRetries_Throws()
    {
        var attempts = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            attempts++;
            throw new HttpRequestException("persistent failure");
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://qa.alerts.api.enqa.co") };
        var options = Options.Create(new AlertsApiOptions { DataSource = "live" });
        var client = new AlertsApiClient(httpClient, options, NullLogger<AlertsApiClient>.Instance);

        await Assert.ThrowsAsync<AlertsApiException>(() => client.GetAlertsAsync("u.100", 0, 1));
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task GetAlertsAsync_StubMode_ReadsRealStubFile_GroupedBySiteAndTrimmed()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called in stub mode"));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new AlertsApiOptions
        {
            DataSource = "stub",
            StubFilePath = TestPaths.ResolveRepoRootPath("data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json")
        });
        var client = new AlertsApiClient(httpClient, options, NullLogger<AlertsApiClient>.Instance);

        var result = await client.GetAlertsAsync("salesforce_oas_0014000000gGR2bAAG.2050", 0, long.MaxValue);

        Assert.NotEmpty(result);
        var firstAlert = result.SelectMany(g => g.Alerts).First();
        Assert.False(string.IsNullOrEmpty(firstAlert.Atn));
        Assert.False(string.IsNullOrEmpty(firstAlert.Message));
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
}
