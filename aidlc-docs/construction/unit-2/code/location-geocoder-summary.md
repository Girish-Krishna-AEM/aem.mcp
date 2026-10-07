# Location Geocoder — Code Summary (FR-4 Addendum)

## Files Created

| File | Purpose |
|---|---|
| `unit-2/src/CoordinatorAgent/Models/GeocodeOutcome.cs` | `GeocodeResult` (lat/lon + matched address) and `GeocodeOutcome` discriminated result (`Resolved`/`Ambiguous`/`NotFound`) |
| `unit-2/src/CoordinatorAgent/Services/CensusGeocoderOptions.cs` | Options class: `BaseUrl` (default `https://geocoding.geo.census.gov/geocoder/`), `TimeoutSeconds` (default `10`) |
| `unit-2/src/CoordinatorAgent/Services/ILocationGeocoder.cs` | `GeocodeAsync(string locationText, CancellationToken)` contract |
| `unit-2/src/CoordinatorAgent/Services/LocationGeocoder.cs` | Implementation: cache-aside via `IMemoryCache` (1hr TTL, lowercase+trim cache key), calls Census Geocoder's `/locations/onelineaddress` endpoint, manual retry loop (3 attempts, 500ms fixed delay) on `HttpRequestException`/`TaskCanceledException`, maps 0/1/>1 address matches to `NotFound`/`Resolved`/`Ambiguous` |
| `unit-2/tests/CoordinatorAgent.Tests/Services/LocationGeocoderTests.cs` | Mocked-`HttpMessageHandler` tests: single match, ambiguous match, not-found, timeout-then-retry-succeeds, all-attempts-fail, cache-hit-skips-second-call |

## Design Notes
- Reuses the existing `FakeHttpMessageHandler` test-double pattern already established in `McpClientTests.cs` for consistency.
- `x`/`y` fields in the Census Geocoder's JSON response map to longitude/latitude respectively (standard GIS coordinate-pair convention) — mapped explicitly in `LocationGeocoder` to `GeocodeResult.Longitude`/`Latitude`.
- No new NuGet packages required.
