# Business Logic Model — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

## Overview
The existing `get_informer_status` MCP tool is reimplemented (same name, new contract) to perform a two-step lookup: (1) search a cached informer list for a free-text match, (2) if a single, enabled informer is resolved, fetch its real alert history for a caller-supplied UTC time range. Two independent external dependencies are introduced — an informer-list API and an alerts-by-user-and-time-range API — each with its own `stub`/`live` mode switch, mirroring `WeatherForecastApiClient` (FR-5) and `LocationGeocoder` (FR-4) conventions.

## Component: `InformerDirectory` (replaces `InformerStatusModule`)

### Responsibility
Given a free-text search string and an optional UTC time range, return an `InformerStatusResult` (`Found`/`Disabled`/`Ambiguous`/`NotFound`).

### Processing Flow
1. **Retrieve informer list** (BR-1/BR-2): check the 1-hour cache; on miss, fetch from `INFORMER_API_BASE_URL` (live) or read `data/informer/informer_list.json` (stub), per `INFORMER_DATA_SOURCE`. Cache the full list under a single fixed key.
2. **Search** (BR-3/BR-4): case-insensitive substring match the search text against `custName`, `informName`, `macAddress`, and `informId` (as string) across the full list, including disabled records.
3. **Branch on match count**:
   - **0 matches** → return `InformerSearchOutcome.NotFound` (BR-6) → tool returns `InformerStatusResult.NotFound`.
   - **>1 matches** → return `InformerSearchOutcome.Ambiguous` with all candidates (BR-7) → tool returns `InformerStatusResult.Ambiguous`, no Alerts call made.
   - **Exactly 1 match** → proceed to step 4.
4. **Disabled check** (BR-5): if the matched informer's `IsDisabled` is `true`, return `InformerStatusResult.Disabled` immediately — no Alerts API call.
5. **Resolve time range** (BR-11): use caller-supplied `startDateTimeUtc`/`endDateTimeUtc` (epoch seconds) if both present; otherwise default to the last 24 hours ending now (UTC).
6. **Call Alerts API** (BR-8): using the matched informer's `PasUserId`, call `ALERTS_API_BASE_URL` + `/api/alerts/v1/ByUserIdAndTimeRange` (live) or read `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json` (stub), per `ALERTS_DATA_SOURCE`, passing `startDateTimeUtc`, `endDateTimeUtc`, `compact=true`, `includeUserAuditEvents=true`, `userId=<PasUserId>`.
7. **Map response** (BR-9/BR-10/BR-12): for each site (`r[]`), build an `AlertSiteGroup` with its `nm` as `SiteName` and its `a[]` alerts trimmed to `AlertRecord` (BR-10). An empty overall alert set (across all sites) is a valid, non-error `Found` result with empty groups (BR-12).
8. Return `InformerStatusResult.Found` with the matched `InformerSearchMatch` and the list of `AlertSiteGroup`.

### Consumers
- **Informer-alert intent** (new, Unit 2 `IntentClassifier`/`QueryController`) — the sole caller of this tool. No other Unit 1 tool or Unit 2 intent depends on `InformerDirectory`.

### Resilience (concrete values fixed in NFR Design)
Both the informer-list fetch and the alerts fetch (when in `live` mode) follow the same timeout/retry convention already established by `LightningPulseApiClient`/`WeatherForecastApiClient`/`LocationGeocoder` — a bounded number of attempts with backoff before surfacing a clear error, rather than hanging indefinitely or silently returning empty data.

### Error Surfacing
`Ambiguous`, `NotFound`, and `Disabled` outcomes each produce a single descriptive plain-language string (BR-14), consistent with the `{ "error": "<string>" }` / plain-text convention already used elsewhere in this project (FR-4 BR-10). No upstream payloads, error codes, or internal exception details are surfaced to the caller.
