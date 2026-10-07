# NFR Design Patterns — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

## Resilience Pattern: Manual Retry Loop
- `LocationGeocoder` performs up to 3 total attempts against whichever provider is currently being called (Census Geocoder first, Nominatim as fallback), with a fixed 500ms `Task.Delay` between attempts, on `HttpRequestException` or `TaskCanceledException` (timeout).
- Implemented as a plain loop inside `LocationGeocoder` — no new dependency (Polly) introduced, consistent with the rest of the codebase having no resiliency library.
- After the 3rd failed attempt (for whichever provider was being called), the failure propagates up as an exception, caught by `QueryController`'s existing `catch` blocks and surfaced as a `503` (same pattern already used for MCP Server unavailability) — no new exception-handling pattern needed at the controller layer, only a new call site inside `ParameterExtractor`/`QueryController`'s existing try/catch needs to also cover geocoder failures (see `logical-components.md`).

## Fallback Pattern: Census-First, Nominatim-on-Not-Found *(Added 2026-10-06)*
- Census Geocoder is address-range-only and cannot resolve City+State or bare-ZIP input (confirmed via live testing, not assumption) — Nominatim is called only when Census returns zero matches, never on Census's own transient-failure path (that still goes through the retry loop above against Census itself before falling back).
- Nominatim requests set a descriptive `User-Agent` header and `countrycodes=us`, per BR-11.
- Both providers' HTTP calls share the same retry-loop implementation (parameterized by base URL/response-parsing logic), avoiding duplicated resilience code.

## Performance Pattern: Cache-Aside
- `IMemoryCache` is checked first (`TryGetValue`); on miss, the Census Geocoder is called, and the result is written back to the cache (`Set` with `AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)`) before returning.
- All three outcome types (`Resolved`, `Ambiguous`, `NotFound`) are cached identically — the cache stores the `GeocodeOutcome` itself, not just successful results.
- No manual eviction/size-bound logic needed — `IMemoryCache`'s default behavior (no explicit size limit configured) matches the "unbounded, POC-acceptable" NFR decision.

## Configuration Pattern: Options
- `CensusGeocoderOptions` class (`BaseUrl`, `TimeoutSeconds`) bound via `IOptions<CensusGeocoderOptions>`, registered via `builder.Services.Configure<CensusGeocoderOptions>(...)` in `Program.cs` using env-var overrides — mirrors Unit 1's `LightningPulseApiOptions` pattern exactly.
  - Defaults: `BaseUrl = "https://geocoding.geo.census.gov/geocoder/"`, `TimeoutSeconds = 10`.
- **Added 2026-10-06**: `NominatimOptions` class (`BaseUrl`, `TimeoutSeconds`, `UserAgent`), same pattern.
  - Defaults: `BaseUrl = "https://nominatim.openstreetmap.org/"`, `TimeoutSeconds = 10`, `UserAgent = "LightningDetectionCoordinatorAgent/1.0 (POC)"`.

## Dependency Injection Pattern: Interface + Two Named HTTP Clients
- `ILocationGeocoder` / `LocationGeocoder` registered via `builder.Services.AddHttpClient<ILocationGeocoder, LocationGeocoder>()` for the **Census** call — the typed-client registration form, which both wires up `IHttpClientFactory` under the hood and makes that `HttpClient` injectable directly into `LocationGeocoder`'s constructor.
- **Added 2026-10-06**: a second, separately-named client (`builder.Services.AddHttpClient("NominatimGeocoder", ...)`) for the Nominatim fallback, since it needs a different base URL, and a `User-Agent` header the Census client doesn't need. `LocationGeocoder` takes an additional `IHttpClientFactory` constructor dependency to resolve this named client on demand (only when the Census call returns zero matches).
- `ParameterExtractor` takes `ILocationGeocoder` as a constructor dependency (alongside its existing `ILogger<ParameterExtractor>`), consistent with how `QueryController` already takes `IIntentClassifier`/`IParameterExtractor`/`IMcpClient` as interfaces.

## Security
- Both Census Geocoder and Nominatim are called over HTTPS; no API key/credentials required for either (public, unauthenticated endpoints) — no secret management concerns introduced.
- No PII is sent — only the free-text location string the user already typed (same trust boundary as today's hardcoded-dictionary lookup), consistent with NFR-4 (Security, deferred) in `requirements.md`.
