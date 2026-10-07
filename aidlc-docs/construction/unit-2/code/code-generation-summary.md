# Code Generation Summary — Unit 2 (Coordinator Agent) — FR-4 Addendum

## Created
- `unit-2/src/CoordinatorAgent/Models/GeocodeOutcome.cs`
- `unit-2/src/CoordinatorAgent/Services/CensusGeocoderOptions.cs`
- `unit-2/src/CoordinatorAgent/Services/NominatimOptions.cs` *(added 2026-10-06, post live-testing)*
- `unit-2/src/CoordinatorAgent/Services/ILocationGeocoder.cs`
- `unit-2/src/CoordinatorAgent/Services/LocationGeocoder.cs` — now tries Census Geocoder first, falls back to Nominatim (`limit=1`, top-ranked result only) when Census returns zero matches
- `unit-2/tests/CoordinatorAgent.Tests/Services/LocationGeocoderTests.cs`

## Modified
- `unit-2/src/CoordinatorAgent/Services/IParameterExtractor.cs` — async signature
- `unit-2/src/CoordinatorAgent/Services/ParameterExtractor.cs` — `KnownLocations` dictionary removed; location resolution now goes through `ILocationGeocoder` for both Strike and Weather intents; new location-phrase extraction regex; 100-mile radius cap check added
- `unit-2/src/CoordinatorAgent/Services/IntentClassifier.cs` *(added 2026-10-06, post live-testing, confirmed with user)* — added "lx" as a recognized Strike-intent keyword synonym
- `unit-2/src/CoordinatorAgent/Controllers/QueryController.cs` — awaits `ExtractParameters`
- `unit-2/src/CoordinatorAgent/Program.cs` — DI registrations for `IMemoryCache`, `CensusGeocoderOptions`/`NominatimOptions`, the typed `ILocationGeocoder`/`LocationGeocoder` HTTP client, and the named `"NominatimGeocoder"` HTTP client (with `User-Agent` header)
- `unit-2/tests/CoordinatorAgent.Tests/Services/ParameterExtractorTests.cs` — converted to async, injects a mocked `ILocationGeocoder`, adds new test cases for free-text location + "around" radius phrasing, ambiguous match, not-found, and over-cap radius
- `unit-2/tests/CoordinatorAgent.Tests/Services/IntentClassifierTests.cs` — added test case for "LX" keyword
- `unit-2/tests/CoordinatorAgent.Tests/Integration/CoordinatorIntegrationTests.cs` — now also fakes the Nominatim HTTP client to guarantee no real network calls in the test suite

## No New NuGet Packages
`IHttpClientFactory`/typed clients and `IMemoryCache` are both part of the ASP.NET Core shared framework already referenced by `CoordinatorAgent.csproj`. Tests reuse the already-referenced `Moq` package and the existing `FakeHttpMessageHandler` test-double pattern.

## No Changes
- Unit 1 (`LightningMcpServer`/`LightningCommon`) — unaffected, not touched.
- Unit 3 (Infrastructure Orchestration) — `docker-compose.yml`/Dockerfiles unchanged; the two new environment variables (`CENSUS_GEOCODER_BASE_URL`, `CENSUS_GEOCODER_TIMEOUT_SECONDS`) are optional with working defaults, so no compose/deployment file change is required for the stack to function. They are documented in `.env.example`/`DEPLOYMENT.md` for discoverability only (see below).

## Verification
- `dotnet build` succeeded for `CoordinatorAgent.csproj` (0 warnings, 0 errors) and for Unit 1's `LightningMcpServer.csproj` (regression check — confirms Unit 1 remains untouched and unaffected).
- `dotnet test` for `CoordinatorAgent.Tests`: all 51 tests pass (was 48 before the Nominatim-fallback and LX-keyword additions below).
- One existing integration test (`CoordinatorIntegrationTests.Query_StrikeIntent_ReturnsFormattedResult`) initially failed because it exercises the full DI stack and was making a real network call to the Census Geocoder; fixed by also faking the geocoder's typed `HttpClient` in the test's `WebApplicationFactory` setup (mirroring the existing fake-MCP-handler pattern).
- **Live Docker Compose verification (not mocked)** surfaced two real external-API-integration bugs that mocked tests could not catch:
  1. Census Geocoder's `/locations/onelineaddress` endpoint is address-range-only — confirmed via direct `curl` that it cannot resolve City+State or bare-ZIP input (returns zero matches even for well-formed queries), only full street addresses. This broke the project's own headline example. **Fixed**: added Nominatim as a fallback when Census returns zero matches (confirmed with user via clarifying question).
  2. Nominatim's default results surfaced multiple same-state sub-localities (5 different "Germantown, Maryland" places) as a false ambiguous-match prompt for a query a human would consider unambiguous. **Fixed**: request Nominatim's single top-ranked result (`limit=1`) instead of multiple candidates (confirmed with user via clarifying question).
  3. `IntentClassifier.cs` didn't recognize "LX" as a Strike-intent keyword, so the user's exact literal example phrase failed at intent classification before even reaching the geocoder. **Fixed**: added "lx" to the Strike regex pattern (confirmed with user as a small scope addition beyond FR-4's original boundary).
- Final live verification: `"Get all the LX for Germantown MD around 50 miles"` (the exact original example) now resolves correctly end-to-end against the real running Docker Compose stack, with correct fallback-to-Nominatim behavior visible in structured logs.

## Traceability
- FR-4 (`requirements.md`) — fully implemented
- Acceptance Criteria #7, #8, #9 — covered by the new/updated test cases in `ParameterExtractorTests.cs` and `LocationGeocoderTests.cs`
