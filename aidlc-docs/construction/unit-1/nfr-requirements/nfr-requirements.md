# NFR Requirements — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

## Performance
- Each individual HTTP call (`live` mode) to the Informer-list API and to the Alerts API MUST time out after **10 seconds** — matches `LocationGeocoder` (FR-4) and `WeatherForecastApiClient` (FR-5).
- Informer-list cache hits (BR-2) MUST short-circuit before any HTTP/file read — sub-millisecond, in-process lookup.
- `stub` mode file reads (BR-1/BR-8) are local disk reads only — no timeout/retry policy applies to them (failure is a startup/config error, not a transient condition).

## Reliability / Resilience
- On timeout or transient HTTP failure (`live` mode only), retry up to 2 additional times (3 attempts total) with a **fixed 500ms delay** between attempts — same fixed-backoff convention as FR-4's `LocationGeocoder` (no exponential backoff; Resiliency Baseline extension remains not opted in — see `requirements.md` Extension Configuration).
- After all 3 attempts fail for either endpoint, surface a clear service-unavailable-style error from the `get_informer_status` tool, consistent with this project's existing upstream-failure-surfacing convention (FR-5's `WeatherForecastApiException` handling) — never silently return empty/stale data.
- **Correction (2026-10-17)**: `LightningPulseApiClient`/`WeatherForecastApiClient` (Unit 1's existing HTTP clients) have **no retry logic today** — a single failed attempt throws immediately. This FR-6 feature introduces the *first* retry logic in Unit 1, borrowed from Unit 2's `LocationGeocoder` (FR-4) rather than from an existing Unit 1 precedent (there wasn't one). Earlier wording in this document incorrectly implied an existing Unit 1 retry convention; corrected here.

## Caching
- The informer list is cached under a **single fixed key** (not per-query, unlike `LocationGeocoder`'s per-input-string cache) with a **1-hour TTL** (per FR-6/Q1, BR-2) — naturally bounded in size (one list).
- Alerts API responses are **not cached** — each `get_informer_status` call with alert-lookup needed makes a fresh Alerts API call (or stub-file read), since the time range varies per query and alert state changes frequently (unlike the relatively static informer list).

## Tech Stack / Integration
- Both new HTTP calls (`live` mode) MUST use `IHttpClientFactory` with named/typed clients registered in `Program.cs`, consistent with `LightningPulseApiClient`/`WeatherForecastApiClient`/`LocationGeocoder`.
- Two new Options-pattern config classes (`InformerApiOptions`, `AlertsApiOptions`), each with a base URL property and a `DataSource` (`stub`|`live`) property — mirrors `CensusGeocoderOptions`/`NominatimOptions` (FR-4) and `WeatherForecastApiOptions` (FR-5) structurally.
- Stub-mode file paths (`data/informer/informer_list.json`, `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json`) are read relative to the application's working directory (consistent with how the project's Docker images already copy the repo root context — no new volume mount needed; `data/` is included in the build context alongside `unit-1/`/`unit-2/`).

## Testability
- All new unit tests for `InformerDirectory` MUST be pure unit tests covering: single match (enabled), single match (disabled, alerts call skipped), ambiguous match, not-found, no-alerts-in-range, default-24h-time-range, and `live`-mode timeout/retry paths (mocked `HttpMessageHandler`) — same convention as `LocationGeocoderTests`/`WeatherForecastModuleTests` (no real network calls in the test suite).
- `stub`-mode tests exercise the actual stub JSON files under `data/informer/` directly (parsing + search + alert-mapping logic), giving fast, deterministic coverage of the real data shape without mocking — this is in addition to, not instead of, the mocked `live`-mode HTTP tests above.
- Per [[project_live_api_verification_required]], a live Docker Compose verification pass against the stub files (not the real off-hours-unavailable endpoints) is still required at Build and Test, since unit tests alone do not confirm the MCP tool registration/wiring end-to-end.

## Observability (POC-level, consistent with NFR-3 in requirements.md)
- Log (`Information`): informer-list cache hit/miss, search match count, which branch was taken (`Found`/`Disabled`/`Ambiguous`/`NotFound`), and each Alerts API attempt (including retries) — matches existing `ParameterExtractor`/`LocationGeocoder` logging style.
- Log (`Warning`): `Ambiguous`, `NotFound`, and `Disabled` outcomes (mirrors existing `LogWarning` usage for missing/unresolvable parameters).
- Log (`Error`): final failure after retries exhausted, for either endpoint.
