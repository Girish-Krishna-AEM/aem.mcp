using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LightningMcpServer.ExternalApis;
using Xunit;

namespace LightningMcpServer.Tests.ExternalApis;

public class LightningPulseApiClientTests
{
    [Theory]
    [InlineData("miles", "mi")]
    [InlineData("km", "km")]
    public async Task GetPulsesAsync_SendsPointAndRadiusWithUnitSuffix(string radiusUnit, string expectedApiUnit)
    {
        Uri? capturedUri = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":200,\"pulses\":[]}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://qa.lxneartime.api.enqa.co") };
        var options = Options.Create(new LightningPulseApiOptions { ApiKey = "test-key" });
        var client = new LightningPulseApiClient(httpClient, options, NullLogger<LightningPulseApiClient>.Instance);

        var start = new DateTime(2026, 10, 8, 18, 50, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 8, 19, 0, 0, DateTimeKind.Utc);

        await client.GetPulsesAsync(start, end, null, 25.7907, -80.1300, 100, radiusUnit);

        Assert.NotNull(capturedUri);
        var query = capturedUri!.Query;
        Assert.Contains("p=25.7907,-80.13", query);
        Assert.Contains($"radius=100{expectedApiUnit}", query);
    }

    [Fact]
    public async Task GetPulsesAsync_WithPulseType_IncludesTypeParameter()
    {
        Uri? capturedUri = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"code\":200,\"pulses\":[]}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://qa.lxneartime.api.enqa.co") };
        var options = Options.Create(new LightningPulseApiOptions { ApiKey = "test-key" });
        var client = new LightningPulseApiClient(httpClient, options, NullLogger<LightningPulseApiClient>.Instance);

        await client.GetPulsesAsync(null, null, 0, 30.2672, -97.7431, 50, "km");

        Assert.Contains("type=0", capturedUri!.Query);
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
