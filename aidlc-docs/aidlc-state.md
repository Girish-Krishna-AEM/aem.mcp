# AI-DLC Project State

## Project
- **Name**: Lightning Detection MCP Server & Coordinator Agent (POC)
- **Type**: Greenfield
- **Current Phase**: 🟡 OPERATIONS PHASE
- **Current Stage**: Operations — EC2 deployment in progress (iterating on `deploy-ec2.sh` against a real, repurposed instance)

## Process Note (added 2026-10-08)
- FR-5 (real daily/hourly forecast tools, commit `097ab9d`) and the initial EC2 deployment script work (commits `2c05c19`..`18e4fa2`) were implemented directly without going through AI-DLC stage gates or contemporaneous audit logging. This has been retroactively reconciled on 2026-10-08 — see `requirements.md` FR-5 and the audit.md entries dated 2026-10-08.
- **Going forward**: any code change made in a session (whether or not it was driven by a formal AI-DLC stage) MUST be reflected in `aidlc-state.md` and `audit.md` before the session ends or hands off (see `session.md` handoffs) — not deferred to "next time the user asks." See [[feedback_sync_aidlc_docs_every_session]].

## Execution Plan Summary (Addendum: FR-4 Location Geocoding Utility)
- See `aidlc-docs/inception/plans/execution-plan.md` for full detail/rationale.
- **Corrected 2026-10-06**: FR-4 is owned entirely by **Unit 2** (`CoordinatorAgent`), not Unit 1. `CoordinatorAgent` has no project reference to `LightningCommon` and runs as a separate container; Unit 1's MCP tool already accepts lat/lon directly and needs no change.
- **Corrected 2026-10-06 (during Build and Test)**: live testing (not mocked) found the US Census Geocoder cannot resolve City+State or bare-ZIP input (address-range-only) — added Nominatim as a fallback, limited to its top-ranked result (`limit=1`) to avoid false-ambiguous prompts from same-state sub-localities. Also added "lx" as a recognized Strike-intent keyword in `IntentClassifier.cs` (small scope addition beyond FR-4, confirmed with user) so the user's literal original example phrase works end-to-end.
- Re-entered stages: Unit 2 Functional Design, NFR Requirements, NFR Design, Code Generation; Build and Test.
- Skipped: Application Design, Units Generation (no new units), Unit 2 Infrastructure Design. Unit 1 not re-opened at all.

## Execution Summary (Addendum: FR-5 Real Daily/Hourly Weather Forecast Tools — retroactively logged 2026-10-08)
- Implemented and committed 2026-10-07 (`097ab9d`) without a formal plan/approval cycle; reconciled retroactively.
- **Unit 1** (`LightningMcpServer`): new `WeatherForecastApiClient`/`IWeatherForecastApiClient` + options/models, two new MCP tools (`get_daily_weather_forecast`, `get_hourly_weather_forecast`) registered in `ToolRegistry.cs`/`McpTools.cs`, `WeatherForecastModule` extended; original stub `get_weather_forecast` (FR-1.2) untouched. Tests added to `WeatherForecastModuleTests.cs`/`StrikeDetectionModuleTests.cs`.
- **Unit 2** (`CoordinatorAgent`): `QueryController`/`ParameterExtractor`/`McpClient`/`ResultFormatter` updated for dynamic Weather-intent routing (hourly → real hourly tool, "15-day" → legacy stub, default → real daily tool), reusing existing `ILocationGeocoder`. Tests added to `ParameterExtractorTests.cs`/`CoordinatorIntegrationTests.cs`.
- No new units; no infra changes. Build/test status at commit time: not separately logged (retroactive reconstruction) — re-verify current `dotnet test` pass count next time either unit is touched.
- **2026-10-08 fix**: `get_hourly_weather_forecast`'s `searchString` path now falls back from a full street address to a derived "City, State" string on upstream `WeatherForecastApiException` (upstream `BySearch` endpoint 500s on full addresses — `Index was outside the bounds of the array`). See `WeatherForecastModule.GetHourlyForecastBySearchWithFallback`/`TryDeriveCityStateFallback` and their tests. Unit 1: 52/52 `dotnet test` passing. Live Docker/round-trip verification still outstanding (local Kestrel blocked by this dev machine's IIS port reservation, unrelated to the fix) — do before/during next EC2 deployment validation.
- **2026-10-08 fix (FR-1.1)**: `ParameterExtractor` (Unit 2) was treating the user's natural-language date/time ("yesterday", explicit "between X and Y") as if it were already UTC, but the real Lightning Pulse API requires UTC and the user's query is always in their own local time. Fixed via a new `TimeZoneInfo` dependency (`USER_TIME_ZONE_ID` env var, default `America/New_York`) that converts parsed local wall-clock times to UTC. Unit 2: 62/62 `dotnet test` passing. **Fully live-verified** via a full `docker compose up`/`down` cycle against the real Lightning Pulse API — confirmed correct UTC offsets for both "yesterday" and explicit date ranges.
- **2026-10-08 fix (FR-1.1, Unit 1)**: discovered (via the real API's live swagger spec + empirical testing, and user-reported missing request params in logs) that `LightningPulseApiClient`/`StrikeDetectionModule` never sent `p` (point)/`radius`/correctly-unit-suffixed radius to the upstream `v1/pulses` endpoint — it fetched an unfiltered-by-location pulse set for the time window and filtered only client-side. Fixed: `GetPulsesAsync` now takes `latitude`/`longitude`/`radius`/`radiusUnit` and sends `p=<lat>,<lon>&radius=<value><mi|km>` (API requires the unit suffix — a bare number returns `"radius is not valid."`). Unit 1: 56/56 `dotnet test` passing (4 new). **Fully live-verified**: the user's exact original query ("Any LX near Miami Beach, FL around 100 miles last 10 minutes") now returns real server-side-filtered strike data (573 strikes) through the full Docker Compose stack against the real upstream API, with the full request URL visible in logs.

## Stage Progress

### 🔵 INCEPTION PHASE
- [x] Workspace Detection
- [N/A] Reverse Engineering (greenfield project)
- [x] Requirements Analysis (incl. 2026-10-06 FR-4 addendum)
- [⏭️] User Stories (SKIP)
- [x] Workflow Planning (incl. 2026-10-06 FR-4 addendum plan)
- [⏭️] Application Design (SKIP — FR-4 addendum: no new component boundaries)
- [x] Units Generation (COMPLETE — FR-4 addendum: SKIP, no new units)

### 🟢 CONSTRUCTION PHASE (Per-Unit Loop)

#### Unit 1: Lightning MCP Server (+ LightningCommon) — NOT re-opened
- No changes required for FR-4; `get_lightning_strikes_near_location` already accepts `latitude`/`longitude` directly. Previously-completed stages stand as-is.

#### Unit 2: Coordinator Agent — re-opened for FR-4 (sole owner)
- [x] Functional Design (COMPLETE — GeocodeOutcome model, BR-1 through BR-10, replaced hardcoded KnownLocations dictionary in ParameterExtractor.cs)
- [x] NFR Requirements (COMPLETE — 10s timeout, fixed 500ms retry backoff, 1hr unbounded cache, IHttpClientFactory, mocked-only tests)
- [x] NFR Design (COMPLETE — manual retry loop, IMemoryCache cache-aside, Options-pattern config mirroring Unit 1, ILocationGeocoder interface)
- [⏭️] Infrastructure Design (SKIP — FR-4 addendum: no new infra resources)
- [x] Code Generation (COMPLETE — LocationGeocoder/ILocationGeocoder/GeocodeOutcome/CensusGeocoderOptions/NominatimOptions created; ParameterExtractor/IParameterExtractor/QueryController/Program.cs/IntentClassifier.cs modified; LocationGeocoderTests.cs created, ParameterExtractorTests.cs + CoordinatorIntegrationTests.cs + IntentClassifierTests.cs updated; all 51 tests passing; Unit 1 regression-build confirmed unaffected)

#### Unit 3: Infrastructure Orchestration
- [⏭️] Functional Design (SKIP)
- [⏭️] NFR Requirements (SKIP)
- [x] NFR Design (COMPLETE)
- [x] Infrastructure Design (COMPLETE)
- [x] Code Generation (COMPLETE — unchanged by FR-4 addendum)

#### Build and Test (After All Units)
- [x] Build and Test (COMPLETE — Phase 1 baseline)
- [x] Build and Test (COMPLETE — FR-4 addendum: 89/89 unit+in-process-integration tests passing; live Docker Compose cross-container verification including real Census Geocoder + Nominatim calls; found and fixed Census-cannot-resolve-city/ZIP and Nominatim-false-ambiguity issues during live testing; exact original example query verified working end-to-end)

### 🟡 OPERATIONS PHASE
- [~] Operations (IN PROGRESS — deploying to a real, repurposed EC2 instance `i-02f1106cb03f558e2` / `10.8.20.224` via `unit-3/scripts/deploy-ec2.sh`)
  - Script iterated 3x against real install errors (commits `2c05c19`, `cc4da0d`, `6a160c3`, `18e4fa2`): dropped blanket `dnf/yum update`; permanently disabled a broken pre-existing `google-chrome` repo; fell back to the official `docker-compose` v2 GitHub-release binary since this AMI's repos don't carry `docker-compose-plugin`.
  - Blocked as of 2026-10-08 on confirming the real git clone path on the instance (likely `~/aem-mcp`, not `/opt/lightning-mcp`) — see `session.md` for literal next steps; user is running the script interactively and pasting output back.
  - Still outstanding: confirm outbound HTTPS access from the instance to `geocoding.geo.census.gov`, `nominatim.openstreetmap.org`, and the internal staging forecast API (`stg.en.ods.forecasts.web.enstg.co`, FR-5) — the latter has shown intermittent `503`s unrelated to deployment.

## Extension Configuration
| Extension | Enabled | Decided At |
|---|---|---|
| Security Baseline | No | Requirements Analysis |
| Resiliency Baseline | No | Requirements Analysis |
| Property-Based Testing | No | Requirements Analysis |

## Notes
- Phase 1 scope deliberately excludes Edge Layer, Authentication, PII Redaction, Observability, Cost Tracker, Agent Evaluation Suite, and Session Store per user decision (see requirements.md Phased Roadmap).
- Extension opt-in answers above were explicitly confirmed by the user during Requirements Analysis (2026-10-04).
- 2026-10-06: New feature requirement added — Location Geocoding Utility (FR-4), re-entering INCEPTION at Requirements Analysis. Requirements document amended; awaiting user approval before Workflow Planning determines which CONSTRUCTION stages (if any) are re-entered for this addition.
