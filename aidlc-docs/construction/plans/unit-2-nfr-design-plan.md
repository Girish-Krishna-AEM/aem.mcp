# NFR Design Plan — Unit 2: Coordinator Agent (FR-4 Addendum: Location Geocoding Utility)

## Plan Steps

- [ ] Decide retry implementation approach (manual loop vs. a library)
- [ ] Decide cache implementation approach (`IMemoryCache` vs. custom dictionary)
- [ ] Decide configuration approach for base URL/timeout (options pattern, consistent with existing `LightningPulseApiClient` in Unit 1)
- [ ] Decide logical component boundaries (interfaces for testability)
- [ ] Document resulting design patterns and logical components

## Clarifying Questions

### Question 1
How should the retry-with-backoff logic (3 attempts, fixed 500ms delay) be implemented?

A) **(Recommended)** A simple manual loop with `Task.Delay(500)` between attempts, inside `LocationGeocoder` itself — no new NuGet dependency, easy to read/test, matches the POC's overall simplicity level and the fact that no resiliency library (e.g. Polly) exists elsewhere in this codebase

B) Add the `Polly` NuGet package for retry policies — more idiomatic for production resilience code, but introduces a new dependency not currently used anywhere else in the solution

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2
How should the 1-hour-TTL in-memory cache be implemented?

A) **(Recommended)** `Microsoft.Extensions.Caching.Memory.IMemoryCache` — built into the ASP.NET Core shared framework already referenced (no new package), thread-safe, has native TTL/expiration support (`AbsoluteExpirationRelativeToNow`), standard DI registration (`AddMemoryCache()`)

B) A custom `ConcurrentDictionary<string, GeocodeCacheEntry>` with manual expiry checks on read — more explicit/visible code, but reinvents what `IMemoryCache` already does correctly

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3
How should the Census Geocoder base URL and timeout be made configurable?

A) **(Recommended)** Mirror the existing `LightningPulseApiOptions`/`IOptions<T>` pattern already used by Unit 1's `LightningPulseApiClient` — a new `CensusGeocoderOptions` class (`BaseUrl`, `TimeoutSeconds`) bound from `appsettings.json`/environment variables, defaulting to the real Census Geocoder URL (`https://geocoding.geo.census.gov/geocoder/`) and 10s so no configuration is required to run out-of-the-box

B) Hardcode the base URL and timeout as constants in `LocationGeocoder` — simpler, but harder to override for testing against a different endpoint or adjusting timeout without a code change

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4
Should `LocationGeocoder` be exposed behind an interface (e.g. `ILocationGeocoder`) for DI/testability, consistent with the existing pattern in this codebase (`IParameterExtractor`, `IMcpClient`, `IIntentClassifier` are all interface + implementation pairs)?

A) **(Recommended)** Yes — add `ILocationGeocoder` + `LocationGeocoder` implementation, registered in DI, injected into `ParameterExtractor` — consistent with every other service in `CoordinatorAgent` and makes `ParameterExtractor`'s own unit tests easy to write with a mocked geocoder

B) No — make it a concrete class instantiated directly inside `ParameterExtractor` — less boilerplate, but inconsistent with the rest of the codebase and harder to unit-test `ParameterExtractor` in isolation

C) Other (please describe after [Answer]: tag below)

[Answer]: A
