# Requirements: Lightning Detection MCP Server & Coordinator Agent (Phase 1 POC)

## Intent Analysis Summary

- **User Request**: Build a Phase 1 proof-of-concept for a Lightning (weather) detection/warning multi-agent system, consisting of a .NET 10/C# MCP Server exposing 4 stub methods (strike/proximity query, weather forecast, sensor diagnostics, informer/strobe-horn status), fronted by a thin Coordinator Agent that routes natural-language user queries to the correct method. Deploy via Docker Compose to a single EC2 instance. This is Phase 1 of a 3-phase roadmap that will later add real data sources and the remaining layers of the reference multi-agent architecture (Edge Layer, Authentication, PII Redaction, Observability, Cost Tracker, Agent Evaluation Suite, Session Store).
- **Request Type**: New Project (greenfield); amended 2026-10-06 with a New Feature addendum (FR-4: Location Geocoding Utility)
- **Scope Estimate**: Multiple Components (MCP Server with 4 tools + Coordinator Agent + containerized deployment), bounded to a single unit of work for Phase 1
- **Complexity Estimate**: Moderate — the Phase 1 implementation itself is simple (stub responses, no real integrations), but the project carries multi-phase architectural and infrastructure implications (AWS deployment, multi-agent pattern, future security/observability layers), warranting Standard-to-Comprehensive requirements detail now.

## Background & Reference Architecture

This POC follows the generic multi-agent reference architecture in `Architecture-1.png`:

User Interface → Edge Layer (WAF/DDoS/Rate Limits/API Gateway) → Authentication → API → PII Redaction → **Coordinator Agent** → domain Agents (e.g. Accounts, Transactions, Service) each paired with its own MCP Server → Session Store, with cross-cutting Authorisation, Observability, Cost Tracker, Agent Evaluation Suite, and a choice of Self-Hosted/Third-party LLM.

For this project, the domain is **Lightning detection and warning** (analogous to Vaisala / Perry Weather / Thor Guard), not physical lighting fixtures. Phase 1 implements only the **Coordinator Agent** and a single domain-specific **Lightning MCP Server**; all other layers in the diagram are explicitly deferred (see Phased Roadmap).

## Functional Requirements

### FR-1: Lightning MCP Server (Service)

A standalone MCP server, implemented in .NET 10.0 / C#, exposing 4 MCP tools. All tools return **stubbed/stuffed representative data** in Phase 1 — no real sensor network, forecast provider, or device telemetry integration exists yet.

#### FR-1.1 — `get_lightning_strikes_near_location`
- **Description**: Returns stubbed lightning strike/proximity data for a given location and radius.
- **Inputs**: `location` (City+State, or ZIP code — accept any one form), `radius` (numeric), `radius_unit` (miles | km)
- **Output (stub)**: A well-formed JSON object/array including: location echo, radius/unit echo, a list of 1-N synthetic strike records (each with approximate lat/long offset from the queried location, distance from location, strike timestamp, estimated intensity/amplitude), and a summary field (e.g. `strike_count`, `nearest_strike_distance`).
- **Business rules**: Reject requests missing both location and radius with a clear validation error (stub-level validation only, no downstream lookup).
- **Time window correction (added 2026-10-08)**: The real upstream Lightning Pulse API requires `startDateTime`/`endDateTime` in UTC. A user's natural-language query (e.g. "yesterday", "between 2026-01-01T10:00:00 and ...") always expresses date/time in the user's own local time, never UTC or an explicit offset — the Coordinator Agent (Unit 2) MUST convert it to UTC before calling this tool, using a configurable IANA timezone (`USER_TIME_ZONE_ID`, default `America/New_York`). Relative windows ("in the last 2 hours") are instant-relative-to-now and need no conversion.
- **Server-side geo-filter correction (added 2026-10-08)**: The real upstream Lightning Pulse API (`v1/pulses`, confirmed via its live `/swagger/v1/swagger.json` and empirical testing) accepts `p` (point, format `"<lat>,<lon>"`), `radius` (numeric value **with a required unit suffix** — `mi`, `km`, or `m`; a bare number is rejected with `"radius is not valid."`), and `type` (`0`=CG, `1`=IC) as server-side filters — in addition to `startDateTime`/`endDateTime`. Unit 1's `LightningPulseApiClient`/`StrikeDetectionModule` MUST send `p`/`radius` (mapping internal `radiusUnit` "miles"→"mi", "km"→"km") so filtering happens server-side, rather than fetching an unfiltered-by-location pulse set for the whole time window and filtering only client-side (which the original implementation did). Client-side distance filtering is retained as a safety net and to compute `nearestStrikeDistance`, but is no longer the sole filter.

#### FR-1.2 — `get_weather_forecast`
- **Description**: Returns a stubbed weather forecast for a given location.
- **Inputs**: `location` (City+State), `forecast_type` (daily | 15-day)
- **Output (stub)**: JSON including location echo, forecast_type echo, and an array of per-day forecast entries (date, condition, high/low temperature, precipitation probability, lightning-risk indicator field relevant to the domain).
- **Business rules**: `forecast_type=daily` returns 1 entry; `15-day` returns 15 entries (synthetic/deterministic stub data is acceptable).

#### FR-1.3 — `get_sensor_diagnostics`
- **Description**: Returns stubbed health/status diagnostics for a lightning detection sensor.
- **Inputs**: `sensor_id` (string identifier)
- **Output (stub)**: JSON including sensor_id echo, and representative attributes:
  - Detection Efficiency (%)
  - GPS Visibility (satellites visible / lock status)
  - Tracked Satellites (count)
  - Noise Level (dB or relative scale)
  - Sensor Uptime (duration or %)
  - Last Calibration Date (ISO date)
  - Signal-to-Noise Ratio
  - Overall Status (e.g. Healthy | Degraded | Offline)

#### FR-1.4 — `get_informer_status`
- **Description**: Returns stubbed status for physical warning devices (strobes/horns, collectively "informers") that activate based on lightning conditions.
- **Inputs**: `informer_id` or `zone` (one of the two, accept either)
- **Output (stub)**: JSON including identifier/zone echo, and representative attributes:
  - Status (Active | Idle | Fault)
  - Last Activation Time (timestamp)
  - Battery/Power Status (e.g. % or On Mains / On Battery)
  - Zone/Location (name or description)
  - Device Type (Strobe | Horn | Combined)

**Naming convention rationale**: tool names use `get_<noun>_<qualifier>` snake_case, matching common MCP tool-naming idioms (self-describing, verb-first, no abbreviations), so the Coordinator Agent's intent-routing/tool-selection can rely on clear, distinct verb+noun semantics.

### FR-2: Coordinator Agent (thin, Phase 1)

- **Description**: A lightweight agent/service that sits in front of the Lightning MCP Server. It receives a natural-language user query, determines user intent, and invokes exactly one of the 4 MCP tools (FR-1.1–FR-1.4) via the MCP protocol, then returns the tool's (stub) result back to the caller, optionally with minimal natural-language framing.
- **Inputs**: Free-text user query (e.g. "Is there lightning near Austin, TX within 10 miles?", "What's the 15-day forecast for Denver, CO?", "How's sensor LN-204 doing?", "Is the strobe in Zone 3 working?").
- **Behavior**:
  - Classify intent into one of the 4 known tool categories.
  - Extract parameters required by the target tool (location/radius, location/forecast_type, sensor_id, informer_id/zone) from the query text; where a required parameter is missing, respond asking for it (Phase 1: simple clarification, not full dialogue management).
  - Invoke the selected MCP tool over the MCP protocol (not by direct in-process function call — this exercises the real agent↔MCP-server integration pattern intended for later phases).
  - Return the tool's JSON stub response (optionally wrapped with a short natural-language summary).
- **Out of scope for Phase 1** (explicitly deferred — see Phased Roadmap): multi-turn session/conversation memory, routing across multiple domain agents, authentication/authorization of the caller, PII redaction, cost tracking, observability instrumentation beyond basic logs.

### FR-3: Interfaces

- The MCP Server MUST expose its 4 tools over the standard MCP protocol (stdio or HTTP/SSE transport — implementer's choice, document the choice in Construction phase).
- The Coordinator Agent MUST be independently invocable (e.g. via a simple HTTP endpoint or CLI for the POC) so a user/tester can submit a natural-language query and observe routing + result without needing a full chat UI (chat UI is out of scope for Phase 1; the architecture's "User Interface" and "Edge Layer" boxes are deferred).

### FR-4: Location Geocoding Utility (added 2026-10-06)

- **Description**: A plain internal utility (not an MCP tool — not exposed over the MCP protocol, no separate service/process) that converts a free-text location string into latitude/longitude, for internal use when building parameters for `get_lightning_strikes_near_location` (FR-1.1) and any other tool that accepts a `location`. Added so the Coordinator Agent can resolve a natural-language location (e.g. "Germantown MD", "20874", "123 Main St, Germantown, MD 20874") to lat/lon before — or in place of — passing raw location text downstream.
- **Location**: A new utility class (`LocationGeocoder`) inside **Unit 2 (`CoordinatorAgent`)**, replacing the existing hardcoded `KnownLocations` dictionary in `ParameterExtractor.cs`. *(Corrected 2026-10-06: the original answer to Q2 — shared `LightningCommon` — turned out to be architecturally infeasible without new cross-unit plumbing: `CoordinatorAgent` has no project reference to `LightningCommon` and runs as a fully separate container from `LightningMcpServer`, communicating only over the MCP protocol. The actual free-text location parsing lives in Unit 2's `ParameterExtractor.cs`; Unit 1's `StrikeDetectionModule`/MCP tool already accepts raw lat/lon, not location text — so Unit 1 needs no change. Re-confirmed with the user, who chose to place the utility in Unit 2 only rather than extract a new shared project.)*
- **Provider**: **US Census Bureau Geocoder** (`geocoding.geo.census.gov`) as primary, with **Nominatim** (OpenStreetMap) as fallback. *(Corrected 2026-10-06, during live Build-and-Test verification: the Census Geocoder's `/locations/onelineaddress` endpoint is address-range-only — it resolves full street addresses correctly but returns zero matches for City+State or bare-ZIP input, confirmed against the real API, not just mocked tests. This directly broke the original example query and Acceptance Criteria #7. Fix, confirmed with the user: try Census Geocoder first — works well for full addresses, no documented rate limit — and fall back to Nominatim (also free; requires a custom User-Agent header and respects its ~1 req/sec usage policy, comfortably covered by the existing 1-hour cache and low POC traffic) only when Census returns zero matches, which covers City+State and ZIP-only input.)*
- **Inputs supported** (any one form):
  - City + State (e.g. "Germantown MD")
  - ZIP code (e.g. "20874")
  - Full street address (e.g. "123 Main St, Germantown, MD 20874")
- **Output**: Resolved `latitude`, `longitude` (and, where available from the provider, the matched/normalized address string, for echoing back to the user).
- **Geographic scope**: **US-only** for Phase 1 (matches overall Phase 1 scope). Non-US input is an error case (treated as "not found").
- **Ambiguous match handling**: If the geocoder returns multiple candidate matches for the input, the utility MUST surface all candidates (not silently pick one); the caller (Coordinator Agent) returns a `400`-equivalent error response to the user listing the candidate matches so they can disambiguate (e.g. "Did you mean: Springfield, IL or Springfield, MO?").
- **Not-found handling**: If the geocoder returns no match, return a clear error (`400`-equivalent) indicating the location could not be resolved.
- **Caching**: Results MUST be cached in-memory (e.g. dictionary keyed by normalized input string) with a time-to-live (TTL); cache is process-local and cleared on restart — no new infrastructure (e.g. Redis) introduced for this in Phase 1.
- **Radius parameter handling** (used together with the resolved lat/lon when calling FR-1.1):
  - Both **miles** and **kilometers** MUST be supported as the radius unit, detected from the user's phrasing (e.g. "50 miles" vs "80 km") or an explicit `radius_unit` parameter.
  - Radius MUST be capped at a maximum of **100 miles** (or the km-equivalent, ~160.9 km) — requests exceeding this cap are rejected with a clear validation error rather than silently clamped.
- **Resilience**: On a timeout or transient failure calling the geocoder, the utility MUST retry up to **2 additional times** (i.e. up to 3 attempts total) with backoff between attempts before surfacing a clear error to the caller. (Note: this is a point resilience behavior scoped to this utility; it does not imply the broader Resiliency Baseline extension — see NFR-4 — has been opted in.)

### FR-5: Real Daily & Hourly Weather Forecast Tools (added 2026-10-07, retroactively logged 2026-10-08)

- **Description**: Two new MCP tools in **Unit 1 (`LightningMcpServer`)** — `get_daily_weather_forecast` (accepts ZIP code or lat/lon) and `get_hourly_weather_forecast` (accepts lat/lon or free-text location search) — backed by a real external forecast API (`WeatherForecastApiClient`, config-driven base URL + endpoint paths, no API key required), trimming the upstream response to abbreviated human-readable fields and surfacing upstream `Code`/`ErrorMessage` envelope errors instead of silently returning empty data. The original stub `get_weather_forecast` tool (FR-1.2) is untouched and still served for legacy "15-day forecast" style requests.
- **Coordinator routing (Unit 2)**: `QueryController`/`ParameterExtractor` now route Weather-intent queries dynamically — "hourly" phrasing → real hourly tool, "15-day" phrasing → legacy mock tool, default → real daily tool — reusing the existing `ILocationGeocoder` (FR-4) for all three, no new geocoding logic introduced.
- **External dependency**: Internal staging forecast API (`http://stg.en.ods.forecasts.web.enstg.co`). Its `locations` sub-dependency has shown intermittent/persistent `503 Service Temporarily Unavailable` outages outside business hours — this is an upstream issue, not a defect in this project's code, and is surfaced to the caller as a clear error rather than masked.
- **Process note**: This feature was implemented and committed (`097ab9d`, 2026-10-07T22:30:17) directly, without first going through the AI-DLC Functional Design / NFR / Code Generation stage gates or contemporaneous `audit.md` logging. It is being retroactively logged here and in `aidlc-state.md`/`audit.md` on 2026-10-08 so the AI-DLC artifacts are back in sync with the actual codebase. See [[feedback_sync_aidlc_docs_every_session]] for the process change adopted to prevent recurrence.

## Non-Functional Requirements

### NFR-1: Technology Stack
- MCP Server and Coordinator Agent implemented in **.NET 10.0, C#**.

### NFR-2: Containerization & Deployment
- Both the Lightning MCP Server and the Coordinator Agent MUST be packaged as Docker images.
- A **Docker Compose** definition MUST orchestrate both services together for local and EC2 execution (this artifact itself is produced in the CONSTRUCTION phase, not here — see Design Decision above).
- Target runtime environment: a **single AWS EC2 instance** running Docker + Docker Compose. No auto-scaling, load balancing, or multi-instance orchestration (e.g. ECS/EKS) is in scope for Phase 1.
- **Open NFR** (no answer provided yet): specific EC2 instance type, region, AMI/base OS, and security group configuration are not yet specified; treat as implementation-time decisions for Infrastructure Design in CONSTRUCTION, using reasonable low-cost defaults (e.g. `t3.small`/`t3.medium`, Amazon Linux 2023 or Ubuntu LTS) unless the user specifies otherwise before that stage.

### NFR-3: Observability (POC-level only)
- Both services MUST emit basic structured console/stdout logs (startup, request received, tool invoked, tool result/stub returned, errors) sufficient for manual verification during the POC.
- Both services MUST expose a basic health-check endpoint (or equivalent MCP "ping"/liveness mechanism) so Docker Compose / EC2 operators can verify the containers are running correctly.
- Full Observability (prompt/agent/tool-call tracing, CPU/memory/disk metrics) per the reference architecture is explicitly **deferred** to a later phase.

### NFR-4: Security (deferred, noted for traceability)
- Phase 1 has **no authentication, authorization, PII redaction, or edge protections** (WAF/DDoS/rate limiting) — the MCP Server and Coordinator Agent are reachable without these controls in this POC.
- No data classified as sensitive/PII is handled in Phase 1 (all responses are synthetic stub data), so encryption-at-rest and PII redaction are **not required for Phase 1** but MUST be planned for in a later phase once real data sources (e.g. real sensor telemetry, real user accounts) are introduced.
- This is a conscious, user-confirmed scope reduction (see Q4/Security-Extension answer in `requirement-verification-questions.md`), not an oversight.

### NFR-5: Maintainability / Extensibility
- The 4 MCP tool implementations SHOULD be structured so that replacing stub logic with a real data source (lightning sensor network API, weather provider API, device telemetry feed) in Phase 2 does not require changing the tool's external contract (input/output schema).
- The Coordinator Agent's intent-routing logic SHOULD be structured so that adding additional domain agents/MCP servers in later phases does not require a rewrite (directionally consistent with the Coordinator→multiple-domain-agents pattern in the reference architecture).

### NFR-6: Performance (POC-level)
- No formal SLAs for Phase 1 given synthetic/stub data; tool calls should return in the sub-second range since no real external I/O is involved.

## Phased Roadmap

### Phase 1 (this requirements document) — MCP Server + Thin Coordinator, Stubbed Data
- Lightning MCP Server (.NET 10/C#) with 4 stub tools (FR-1.1–FR-1.4).
- Thin Coordinator Agent performing intent classification and MCP tool invocation (FR-2).
- Docker + Docker Compose packaging, deployed to a single EC2 instance.
- Explicitly excludes: Edge Layer, Authentication, PII Redaction, Observability, Cost Tracker, Agent Evaluation Suite, Session Store.

### Phase 2 (directional intent — to be detailed in a future Requirements Analysis pass)
- Replace stub implementations with real data integrations:
  - Real lightning strike/proximity data source (e.g. a lightning detection network API).
  - Real weather forecast provider integration.
  - Real (or simulated-but-stateful) sensor diagnostics feed.
  - Real (or simulated-but-stateful) informer/strobe-horn device status feed.
- Introduce a **Session Store** for conversation history / inter-agent state, enabling multi-turn conversations.
- Introduce basic **Authentication** and **API** layer hardening ahead of broader exposure.
- Exact scope, data sources, and design to be defined in a dedicated future Requirements Analysis / Application Design cycle — not specified further here.

### Phase 3 (directional intent — to be detailed in a future Requirements Analysis pass)
- Complete the reference architecture: Edge Layer (WAF/DDoS/Rate Limiting/API Gateway), PII Redaction, Authorisation, Observability (prompt/agent/tool-call tracing, resource metrics), Cost Tracker, Agent Evaluation Suite.
- Evaluate expanding beyond the single-EC2-instance deployment model (e.g. ECS/EKS, multi-AZ) if operational requirements justify it.
- Potentially introduce additional domain agents beyond Lightning (mirroring the Accounts/Transactions/Service pattern in the reference diagram) if the product scope grows.
- Exact scope and design to be defined in a dedicated future Requirements Analysis cycle — not specified further here.

## Acceptance Criteria (Phase 1)

1. Running `docker compose up` (or equivalent) on the target EC2 instance starts both the Lightning MCP Server and the Coordinator Agent containers successfully, and both report healthy via their health-check mechanism.
2. Each of the 4 MCP tools (`get_lightning_strikes_near_location`, `get_weather_forecast`, `get_sensor_diagnostics`, `get_informer_status`) is independently callable via the MCP protocol and returns a well-formed JSON stub response matching its documented schema (FR-1.1–FR-1.4), including for minimally-valid and missing-parameter inputs.
3. Submitting a natural-language query to the Coordinator Agent that clearly maps to one of the 4 intents results in the Coordinator Agent invoking the correct corresponding MCP tool (verified for at least one representative query per tool) and returning its result.
4. Submitting a natural-language query with a missing required parameter (e.g. "is there lightning nearby?" with no location) results in the Coordinator Agent asking for the missing parameter rather than guessing or erroring unhandled.
5. Container and application logs are visible (`docker compose logs`) showing at minimum: service startup, each MCP tool invocation, and any errors.
6. No authentication, PII redaction, or edge-layer controls are present (confirmed absent) — consistent with the agreed Phase 1 scope reduction.
7. Submitting a query with a free-text location (City+State, ZIP, or full street address, e.g. "Germantown MD") to `get_lightning_strikes_near_location` resolves correctly to lat/lon via the Location Geocoding Utility (FR-4) and returns strike data for the resolved coordinates and requested radius (verified for at least one example per supported input format).
8. An ambiguous location (multiple candidate matches) and an unresolvable location each return a clear error response rather than a silent best-guess or unhandled exception.
9. A radius request exceeding 100 miles (or km-equivalent) is rejected with a clear validation error.

## Summary of Key Requirements

Phase 1 delivers a minimal but architecturally-consistent slice of the lightning-detection multi-agent reference pattern: a .NET 10/C# **Lightning MCP Server** exposing 4 stubbed tools (strike proximity, weather forecast, sensor diagnostics, informer/strobe-horn status), fronted by a thin **Coordinator Agent** that performs real MCP-protocol-based intent routing (no hard-coded shortcuts), packaged with Docker Compose and deployed to a single EC2 instance. Security, PII handling, observability, cost tracking, evaluation, and session persistence are intentionally deferred, with Phase 2 adding real data integrations and session state, and Phase 3 completing the remaining architecture layers (Edge, Auth, Observability, Cost Tracker, Evaluation Suite).
