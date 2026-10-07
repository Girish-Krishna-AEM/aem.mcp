# Tech Stack Decisions — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

No new runtime technology is introduced — this stays within the existing Unit 2 stack (.NET 10.0 / C#, ASP.NET Core).

| Decision | Choice | Rationale |
|---|---|---|
| Geocoding provider | US Census Bureau Geocoder (`geocoding.geo.census.gov`), `/locations/onelineaddress` endpoint | Free, no API key, no documented rate limit, US-only scope matches Phase 1 (per requirements.md FR-4 / Q1 answer) |
| HTTP client | `IHttpClientFactory` named/typed client (`System.Net.Http`, built into ASP.NET Core — no new NuGet package) | Standard practice, avoids socket exhaustion, no new dependency |
| Caching | In-memory `Dictionary`/`ConcurrentDictionary`-backed cache with manual TTL check (or `Microsoft.Extensions.Caching.Memory.IMemoryCache`, built into ASP.NET Core) | No new infrastructure (e.g. Redis); built-in options only |
| Testing | xUnit + existing mocking approach already used in `CoordinatorAgent.Tests` (mocked `HttpMessageHandler`) | Consistent with existing test project, no new test framework/library |

**No new NuGet packages are required** for this addendum — `IHttpClientFactory` and `IMemoryCache` are both part of the ASP.NET Core shared framework already referenced by `CoordinatorAgent` (`Microsoft.NET.Sdk.Web`).
