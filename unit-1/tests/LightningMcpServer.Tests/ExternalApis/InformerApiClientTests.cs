using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using LightningMcpServer.ExternalApis;
using Xunit;

namespace LightningMcpServer.Tests.ExternalApis;

public class InformerApiClientTests
{
    [Fact]
    public async Task GetInformersAsync_LiveMode_SuccessfulResponse_ReturnsRecords()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "[{\"custId\":1,\"informId\":100,\"macAddress\":\"AA:BB\",\"informName\":\"Test\",\"pasUserId\":\"u.100\",\"isDisabled\":false,\"custName\":\"Cust\"}]",
                System.Text.Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://qa.oas.api.enqa.co") };
        var options = Options.Create(new InformerApiOptions { DataSource = "live" });
        var client = new InformerApiClient(httpClient, options, NullLogger<InformerApiClient>.Instance);

        var result = await client.GetInformersAsync();

        Assert.Single(result);
        Assert.Equal(100, result[0].InformId);
    }

    [Fact]
    public async Task GetInformersAsync_LiveMode_TimeoutThenRetrySucceeds()
    {
        var attempts = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts < 2) throw new HttpRequestException("transient");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://qa.oas.api.enqa.co") };
        var options = Options.Create(new InformerApiOptions { DataSource = "live" });
        var client = new InformerApiClient(httpClient, options, NullLogger<InformerApiClient>.Instance);

        var result = await client.GetInformersAsync();

        Assert.Empty(result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetInformersAsync_LiveMode_ExhaustsAllRetries_Throws()
    {
        var attempts = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            attempts++;
            throw new HttpRequestException("persistent failure");
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://qa.oas.api.enqa.co") };
        var options = Options.Create(new InformerApiOptions { DataSource = "live" });
        var client = new InformerApiClient(httpClient, options, NullLogger<InformerApiClient>.Instance);

        await Assert.ThrowsAsync<InformerApiException>(() => client.GetInformersAsync());
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task GetInformersAsync_StubMode_ReadsRealStubFile()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called in stub mode"));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new InformerApiOptions
        {
            DataSource = "stub",
            StubFilePath = TestPaths.ResolveRepoRootPath("data/informer/informer_list.json")
        });
        var client = new InformerApiClient(httpClient, options, NullLogger<InformerApiClient>.Instance);

        var result = await client.GetInformersAsync();

        Assert.NotEmpty(result);
        Assert.Contains(result, i => i.InformId == 2049);
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
