# Code Generation Plan — Unit 2: Coordinator Agent (FR-4 Addendum: Location Geocoding Utility)

**Source of truth**: This plan, executed exactly as written. References: `functional-design/` (domain model, business rules), `nfr-requirements/`, `nfr-design/` (patterns, logical components) under `aidlc-docs/construction/unit-2/`.

**Unit dependencies**: None (Unit 1 and Unit 3 are unaffected by this addendum).

**Workspace root for application code**: `C:\aem\ai-agentic\mcp` (brownfield — modifying existing `unit-2/src/CoordinatorAgent` and `unit-2/tests/CoordinatorAgent.Tests` in place, no new project structure).

## Steps

### Step 1 — Domain Models (New Files)
- [x] Create `unit-2/src/CoordinatorAgent/Models/GeocodeOutcome.cs`: `GeocodeResult` (Latitude, Longitude, MatchedAddress) and `GeocodeOutcome` (discriminated: `Resolved(GeocodeResult)` / `Ambiguous(IReadOnlyList<string> Candidates)` / `NotFound`), per `functional-design/domain-entities.md`.

### Step 2 — Configuration (New File)
- [x] Create `unit-2/src/CoordinatorAgent/Services/CensusGeocoderOptions.cs`: `BaseUrl` (default `"https://geocoding.geo.census.gov/geocoder/"`), `TimeoutSeconds` (default `10`) — mirrors `LightningPulseApiOptions`'s property-default style (Unit 1).

### Step 3 — LocationGeocoder (New Files)
- [x] Create `unit-2/src/CoordinatorAgent/Services/ILocationGeocoder.cs`: `Task<GeocodeOutcome> GeocodeAsync(string locationText, CancellationToken cancellationToken = default)`.
- [x] Create `unit-2/src/CoordinatorAgent/Services/LocationGeocoder.cs`: implements cache-aside (`IMemoryCache`, 1hr TTL, key = lowercased+trimmed input) wrapping a call to Census Geocoder `/locations/onelineaddress` (via injected typed `HttpClient` + `IOptions<CensusGeocoderOptions>`), with a manual retry loop (3 attempts, fixed 500ms `Task.Delay`) on `HttpRequestException`/`TaskCanceledException`, mapping 0/1/>1 matches to `NotFound`/`Resolved`/`Ambiguous` per `business-rules.md` BR-3–BR-5, BR-7, BR-9.

### Step 4 — Business Logic Unit Testing
- [x] Create `unit-2/tests/CoordinatorAgent.Tests/Services/LocationGeocoderTests.cs`: mocked `HttpMessageHandler` covering single match (Resolved), multiple matches (Ambiguous with candidate list), zero matches (NotFound), timeout-then-success-on-retry, timeout-exhausts-all-3-attempts (propagates failure), and cache-hit-skips-second-HTTP-call — per NFR Requirements' mocked-only test strategy.

### Step 5 — Business Logic Summary
- [x] Create `aidlc-docs/construction/unit-2/code/location-geocoder-summary.md` documenting the new `LocationGeocoder`/`ILocationGeocoder`/`GeocodeOutcome`/`CensusGeocoderOptions` files and their responsibilities (markdown summary only, per Code Location Rules).

### Step 6 — API/Service Layer Integration (Modify Existing)
- [x] Modify `unit-2/src/CoordinatorAgent/Services/IParameterExtractor.cs`: change `ExtractParameters` return type to `Task<(bool Success, JsonElement? Parameters, string? Error)>`.
- [x] Modify `unit-2/src/CoordinatorAgent/Services/ParameterExtractor.cs`:
  - Add `ILocationGeocoder` constructor dependency.
  - Delete the `KnownLocations` dictionary entirely.
  - Make `TryExtractLocation` async, returning the `GeocodeOutcome` (explicit lat/long regex match still short-circuits with no geocoding call, per BR-2).
  - Make `ExtractStrikeParameters` and `ExtractWeatherParameters` async; branch on `GeocodeOutcome` kind — `Resolved` continues, `Ambiguous` returns the candidate-list error string (BR-4 format), `NotFound` returns the not-found error string (BR-5 format).
  - Add the 100-mile radius cap check in `ExtractStrikeParameters` after `TryExtractRadius` (BR-8), converting km→miles for the comparison; reject with a validation error string if exceeded.
  - Make `ExtractParameters` async (`Task<...>`), awaiting the appropriate branch.
- [x] Modify `unit-2/src/CoordinatorAgent/Controllers/QueryController.cs`: `await _parameterExtractor.ExtractParameters(...)` (controller method is already `async`, so this is a one-line change).

### Step 7 — API/Service Layer Unit Testing (Modify Existing + New)
- [x] Modify `unit-2/tests/CoordinatorAgent.Tests/Services/ParameterExtractorTests.cs`: inject a mocked `ILocationGeocoder` (e.g. via a simple test double/Moq returning fixed `GeocodeOutcome.Resolved` results for "Austin, TX" / "Dallas" to preserve existing assertions), convert all test methods from `void` to `async Task` with `await`, add new test cases for: ambiguous-location error message, not-found-location error message, over-100-mile-radius-cap rejection, and confirm Weather intent also now goes through the geocoder (per functional design BR-1, applies to both Strike and Weather).

### Step 8 — API/Service Layer Summary
- [x] Create `aidlc-docs/construction/unit-2/code/parameter-extractor-update-summary.md` documenting the `ParameterExtractor`/`IParameterExtractor`/`QueryController` changes (markdown summary only).

### Step 9 — Dependency Injection Wiring (Modify Existing)
- [x] Modify `unit-2/src/CoordinatorAgent/Program.cs`:
  - `builder.Services.AddMemoryCache();`
  - `builder.Services.Configure<CensusGeocoderOptions>(options => { options.BaseUrl = builder.Configuration["CENSUS_GEOCODER_BASE_URL"] ?? options.BaseUrl; options.TimeoutSeconds = int.TryParse(builder.Configuration["CENSUS_GEOCODER_TIMEOUT_SECONDS"], out var t) ? t : options.TimeoutSeconds; });` (mirrors Unit 1's `LightningPulseApiOptions` binding style exactly — env vars, not appsettings.json sections).
  - `builder.Services.AddHttpClient<ILocationGeocoder, LocationGeocoder>();`

### Step 10 — Documentation Generation
- [x] Update `aidlc-docs/construction/build-and-test/build-and-test-summary.md` is NOT touched here (handled in the Build and Test stage re-run, not Code Generation) — no action in this step; listed for completeness/traceability only.
- [x] Create `aidlc-docs/construction/unit-2/code/code-generation-summary.md` — overall summary of all files created/modified for this addendum, cross-referencing Steps 1–9.

### Step 11 — Deployment Artifacts
- [x] None required — no new environment variables are mandatory (both `CensusGeocoderOptions` fields have working defaults), no Dockerfile/compose changes needed. `DEPLOYMENT.md`/`.env.example` MAY optionally document the two new optional env vars (`CENSUS_GEOCODER_BASE_URL`, `CENSUS_GEOCODER_TIMEOUT_SECONDS`) for discoverability — included as a small addition in this step for completeness.

## Story / Requirement Traceability
- FR-4 (Location Geocoding Utility) in `aidlc-docs/inception/requirements/requirements.md` — Steps 1–9
- Acceptance Criteria #7 (resolve all 3 input formats) — Steps 3, 6, 7
- Acceptance Criteria #8 (ambiguous/not-found errors) — Steps 3, 6, 7
- Acceptance Criteria #9 (100-mile radius cap) — Step 6, 7

## Estimated Scope
11 steps; ~8 files touched (4 new: `GeocodeOutcome.cs`, `CensusGeocoderOptions.cs`, `ILocationGeocoder.cs`, `LocationGeocoder.cs`; 4 modified: `IParameterExtractor.cs`, `ParameterExtractor.cs`, `QueryController.cs`, `Program.cs`) plus 1 new test file and 1 modified test file, plus 3 markdown summaries and an optional `.env.example`/`DEPLOYMENT.md` touch-up.
