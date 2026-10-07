using System.Net;
using System.Text;
using CoordinatorAgent.Models;
using CoordinatorAgent.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoordinatorAgent.Tests.Services;

public class LocationGeocoderTests
{
    private static HttpResponseMessage CensusNoMatch() => Json("""{"result":{"addressMatches":[]}}""");
    private static HttpResponseMessage NominatimNoMatch() => Json("[]");

    [Fact]
    public async Task GeocodeAsync_CensusSingleMatch_ReturnsResolvedWithoutCallingNominatim()
    {
        var nominatimCallCount = 0;
        var geocoder = CreateGeocoder(
            _ => Json("""
                {"result":{"addressMatches":[{"matchedAddress":"1600 Pennsylvania Ave NW, Washington, DC, 20500","coordinates":{"x":-77.0352,"y":38.8987}}]}}
                """),
            _ => { nominatimCallCount++; return NominatimNoMatch(); });

        var outcome = await geocoder.GeocodeAsync("1600 Pennsylvania Ave NW, Washington, DC 20500");

        Assert.Equal(GeocodeOutcomeKind.Resolved, outcome.Kind);
        Assert.Equal(38.8987, outcome.Result!.Latitude, 4);
        Assert.Equal(-77.0352, outcome.Result.Longitude, 4);
        Assert.Equal(0, nominatimCallCount);
    }

    [Fact]
    public async Task GeocodeAsync_CensusMultipleMatches_ReturnsAmbiguousWithoutCallingNominatim()
    {
        var nominatimCallCount = 0;
        var geocoder = CreateGeocoder(
            _ => Json("""
                {"result":{"addressMatches":[
                    {"matchedAddress":"100 Main St, Springfield, IL, 62701","coordinates":{"x":-89.6501,"y":39.7817}},
                    {"matchedAddress":"100 Main St, Springfield, MO, 65801","coordinates":{"x":-93.2923,"y":37.2090}}
                ]}}
                """),
            _ => { nominatimCallCount++; return NominatimNoMatch(); });

        var outcome = await geocoder.GeocodeAsync("100 Main St, Springfield");

        Assert.Equal(GeocodeOutcomeKind.Ambiguous, outcome.Kind);
        Assert.Equal(2, outcome.Candidates.Count);
        Assert.Equal(0, nominatimCallCount);
    }

    [Fact]
    public async Task GeocodeAsync_CensusNoMatch_FallsBackToNominatimSingleMatch_ReturnsResolved()
    {
        var geocoder = CreateGeocoder(
            _ => CensusNoMatch(),
            _ => Json("""[{"display_name":"Germantown, Montgomery County, Maryland, 20874, United States","lat":"39.1732","lon":"-77.2719"}]"""));

        var outcome = await geocoder.GeocodeAsync("Germantown MD");

        Assert.Equal(GeocodeOutcomeKind.Resolved, outcome.Kind);
        Assert.Equal(39.1732, outcome.Result!.Latitude, 4);
        Assert.Equal(-77.2719, outcome.Result.Longitude, 4);
    }

    [Fact]
    public async Task GeocodeAsync_CensusNoMatch_NominatimReturnsMultipleMatchesAnyway_ReturnsAmbiguous()
    {
        // Defensive case: we request limit=1 from Nominatim (see BR-11.1), so this should not
        // happen in normal operation, but the mapping logic still handles it correctly if it did.
        var geocoder = CreateGeocoder(
            _ => CensusNoMatch(),
            _ => Json("""
                [
                  {"display_name":"Springfield, Illinois, United States","lat":"39.7817","lon":"-89.6501"},
                  {"display_name":"Springfield, Missouri, United States","lat":"37.2090","lon":"-93.2923"}
                ]
                """));

        var outcome = await geocoder.GeocodeAsync("Springfield");

        Assert.Equal(GeocodeOutcomeKind.Ambiguous, outcome.Kind);
        Assert.Equal(2, outcome.Candidates.Count);
    }

    [Fact]
    public async Task GeocodeAsync_BothProvidersNoMatch_ReturnsNotFound()
    {
        var geocoder = CreateGeocoder(_ => CensusNoMatch(), _ => NominatimNoMatch());

        var outcome = await geocoder.GeocodeAsync("Nowhereville XX");

        Assert.Equal(GeocodeOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task GeocodeAsync_CensusTimeoutThenSuccess_RetriesAndResolvesWithoutNominatim()
    {
        var censusCallCount = 0;
        var nominatimCallCount = 0;
        var geocoder = CreateGeocoder(
            _ =>
            {
                censusCallCount++;
                if (censusCallCount == 1)
                {
                    throw new HttpRequestException("transient failure");
                }

                return Json("""
                    {"result":{"addressMatches":[{"matchedAddress":"Austin, TX, 78701","coordinates":{"x":-97.7431,"y":30.2672}}]}}
                    """);
            },
            _ => { nominatimCallCount++; return NominatimNoMatch(); });

        var outcome = await geocoder.GeocodeAsync("Austin, TX");

        Assert.Equal(2, censusCallCount);
        Assert.Equal(0, nominatimCallCount);
        Assert.Equal(GeocodeOutcomeKind.Resolved, outcome.Kind);
    }

    [Fact]
    public async Task GeocodeAsync_CensusAllAttemptsFail_ThrowsAfterThreeAttemptsWithoutCallingNominatim()
    {
        var censusCallCount = 0;
        var nominatimCallCount = 0;
        var geocoder = CreateGeocoder(
            _ => { censusCallCount++; throw new HttpRequestException("persistent failure"); },
            _ => { nominatimCallCount++; return NominatimNoMatch(); });

        await Assert.ThrowsAsync<HttpRequestException>(() => geocoder.GeocodeAsync("Unreachable Town"));

        Assert.Equal(3, censusCallCount);
        Assert.Equal(0, nominatimCallCount);
    }

    [Fact]
    public async Task GeocodeAsync_SecondCallSameInput_UsesCacheSkipsBothProviders()
    {
        var censusCallCount = 0;
        var geocoder = CreateGeocoder(
            _ =>
            {
                censusCallCount++;
                return Json("""
                    {"result":{"addressMatches":[{"matchedAddress":"Denver, CO, 80202","coordinates":{"x":-104.9903,"y":39.7392}}]}}
                    """);
            },
            _ => NominatimNoMatch());

        await geocoder.GeocodeAsync("Denver, CO");
        await geocoder.GeocodeAsync("denver, co");

        Assert.Equal(1, censusCallCount);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static LocationGeocoder CreateGeocoder(
        Func<HttpRequestMessage, HttpResponseMessage> censusResponder,
        Func<HttpRequestMessage, HttpResponseMessage> nominatimResponder)
    {
        var censusHandler = new FakeHttpMessageHandler(censusResponder);
        var censusHttpClient = new HttpClient(censusHandler) { BaseAddress = new Uri("https://geocoding.geo.census.gov/geocoder/") };

        var nominatimHandler = new FakeHttpMessageHandler(nominatimResponder);
        var nominatimHttpClient = new HttpClient(nominatimHandler) { BaseAddress = new Uri("https://nominatim.openstreetmap.org/") };

        var censusOptions = Options.Create(new CensusGeocoderOptions());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClientFactory = new SingleClientHttpClientFactory(nominatimHttpClient);

        return new LocationGeocoder(censusHttpClient, httpClientFactory, censusOptions, cache, NullLogger<LocationGeocoder>.Instance);
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
