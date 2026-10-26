# Business Rules — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

## BR-1: Informer List Retrieval and Mode Switch
The informer list is retrieved from either the real endpoint or the local stub file, selected explicitly by the `INFORMER_DATA_SOURCE` env var (`stub` | `live`) — never automatically on failure (Q1=C). `live` mode calls the configurable `INFORMER_API_BASE_URL` + `/api/oas/Informer/v1/GetInformers`; `stub` mode reads `data/informer/informer_list.json` from disk.

## BR-2: Informer List Caching
The retrieved/loaded informer list MUST be cached in-memory (`IMemoryCache`, cache-aside, single fixed key) with a **1-hour TTL**, mirroring `LocationGeocoder`'s (FR-4) and `WeatherForecastApiClient`'s (FR-5) caching pattern. A cache miss re-fetches/re-reads per BR-1.

## BR-3: Search Matching
Given free-text search input, match against `custName`, `informName`, `macAddress`, and `informId` (numeric, matched against its string representation) using **case-insensitive substring matching on all four fields** (Q2=C — most forgiving). A record matches if the search text is a substring of ANY of the four fields.

## BR-4: Disabled Informers Are Always Searchable
`isDisabled: true` records are never excluded from the search in BR-3 (Q8=B, confirmed). Disabling only affects what happens after a single match is selected (BR-5).

## BR-5: Disabled Informer Short-Circuits Alerts Lookup
If the single informer resolved by Step 1 (BR-3, possibly after a BR-7 disambiguation) has `isDisabled: true`, skip the Alerts API call (Step 2) entirely and return `InformerStatusResult.Disabled` with that informer's identifying details, so the caller can report "this informer is disabled and not operational" instead of attempting (and failing to get useful data from) an alert lookup.

## BR-6: Zero Matches → Not Found, No Suggestions
If BR-3 matches zero informers, return `InformerSearchOutcome.NotFound`. No separate "did you mean...?" close-match algorithm is implemented (Q2 of the Functional Design plan, answer A) — because matching is already substring/fuzzy across all 4 fields, a genuine zero-result case means no reasonable match exists; adding a second fuzzy pass over the same fields would not meaningfully improve on it.

## BR-7: Multiple Matches → Ambiguous, Never Auto-Select or Auto-Aggregate
If BR-3 matches more than one informer, return `InformerSearchOutcome.Ambiguous` with the full list of `InformerSearchMatch` candidates (customer name, informer name, MAC, informer ID, disabled flag) — never auto-pick one and never auto-query/aggregate alerts for all of them (Q3, confirmed: "provide way to pick the selection of available informers"). The caller surfaces the candidate list so the user can re-submit a more specific query (e.g. including the MAC address or informer ID), mirroring Unit 2's existing FR-4 ambiguous-location-match pattern (`GeocodeOutcome.Ambiguous`).

## BR-8: Alerts Lookup Mode Switch and Endpoint
Mirrors BR-1: `ALERTS_DATA_SOURCE` (`stub` | `live`) explicitly selects between calling the configurable `ALERTS_API_BASE_URL` + `/api/alerts/v1/ByUserIdAndTimeRange` endpoint and reading the local stub file `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json`. The real call passes `startDateTimeUtc`, `endDateTimeUtc` (UTC epoch seconds, supplied by the caller — Unit 1 does not parse natural language time phrases itself), `compact=true`, `includeUserAuditEvents=true`, and `userId=<PasUserId>` of the resolved informer.

## BR-9: Alerts Grouped by Site, Not Flattened
The upstream response's per-site structure (`r[].nm` + `r[].a[]`) is preserved as `IReadOnlyList<AlertSiteGroup>` in the result — never flattened into one combined list (Q5=B, confirmed: different sites can mean different physical zones).

## BR-10: Alert Field Trimming
Each returned alert is trimmed to exactly: `atn`, `m`, and all five UTC timestamp fields (`actutc`, `issutc`, `exputc`, `acutc`, `lmutc`) — mapped to `AlertRecord`'s `Atn`/`Message`/`ActionUtc`/`IssuedUtc`/`ExpiresUtc`/`AutoClearUtc`/`LastModifiedUtc` (Q6=A). All other upstream alert fields (local-time duplicates, `uaid`/`id`/`apid`, `read`, `latest`, `uril`, `uat`, `trig`, `mam`) are dropped — never surfaced by this tool.

## BR-11: Time Range Filtering and Default
- `startDateTimeUtc`/`endDateTimeUtc` are supplied by the caller (Unit 2) as UTC epoch seconds, derived from the user's natural-language time phrase (explicit range, "yesterday", "last N days", etc.) using Unit 2's existing local→UTC conversion built for FR-1.1 (Q7=A) — Unit 1 performs no NL time parsing.
- If the caller omits both `startDateTimeUtc` and `endDateTimeUtc` entirely (no time phrase in the user's query at all), Unit 1 defaults the range to **the last 24 hours (last 1 day), ending now (UTC)** (Functional Design plan Q1=A). This default is applied by Unit 1 (not Unit 2), so it remains correct regardless of which caller invokes the tool.

## BR-12: No Alerts Found in Range
If the resolved informer's Alerts API response contains zero alerts across all sites within the requested time range, this is reported as `InformerStatusResult.Found` with an empty `AlertSiteGroups` list — not a distinct error case — so the caller can render a plain "no alerts found for `<informer>` between `<start>` and `<end>`" message (Q9=A's no-raw-payload convention) without a separate code path.

## BR-13: Replaces `get_informer_status` Tool Contract
The `get_informer_status` MCP tool name is retained but its entire input/output contract is replaced (Q4=A) — the prior random-stub `InformerStatus`/`GetInformerStatusRequest` shape (status/power/zone/device-type) is removed, not kept alongside the new contract.

## BR-14: Error Message Convention
All non-success outcomes (`Ambiguous`, `NotFound`, `Disabled`) are surfaced as plain-language messages, consistent with FR-4's `{ "error": "<string>" }` single-string convention (BR-10 in `unit-2/functional-design/business-rules.md`) — no raw upstream payloads or error codes exposed to the caller.

## BR-15: Location-Based Informer Search Mode (Addendum: Multi-Informer Location Search)
A new `get_informers_near_location` MCP tool finds informers within a radius (miles or km) of a given lat/lon, reusing the same cached informer list (BR-2) as the free-text path (BR-3). This is an **additive** tool — BR-13's `get_informer_status` contract is unaffected and remains byte-for-byte unchanged.

## BR-16: Non-Geocoded Informers Excluded from Radius Matching
Stub data shows only a minority of informer records have real (non-zero) `latitude`/`longitude`; the rest are `(0, 0)`. Records with `latitude == 0 && longitude == 0` are treated as "not geocoded" and excluded from radius matching entirely — otherwise they would spuriously appear "near" any location close to (0,0).

## BR-17: Radius Cap and Nearest-25 Truncation
The search radius is capped at **100 miles** (mirroring Unit 2's existing Strike-intent cap) — a request above this limit is rejected with a plain-language error, not silently clamped. When more than 25 informers fall within the radius, only the **25 nearest** (by distance) are returned, with `truncated: true` and `totalMatchesInRadius: <N>` surfaced in the response so the caller knows results were limited.

## BR-18: Disabled Informers in Radius Results
Disabled informers within the search radius are **included** in the result list (flagged `isDisabled: true`, with `distanceMiles` populated), but the Alerts API (BR-8) is **never called** for them — mirroring BR-5's short-circuit for the free-text single-search path.

## BR-19: Location Search Returns Counts Only, Not Alert Detail
Each entry in the location-search response carries only identifying fields (`informId`, `informName`, `custName`, `macAddress`, `isDisabled`, `distanceMiles`) plus `alertCount`/`siteCount` — **never** the full `AlertSiteGroup`/`AlertRecord` detail that BR-9/BR-10 define for the single-search path. Full alert detail for one specific informer remains available only via the unchanged `get_informer_status` tool.

## BR-20: Partial Alerts-Fetch Failure Does Not Fail the Whole Request
If an individual informer's Alerts API call fails during the location-search parallel fan-out, that informer is still returned in the result list (`alertCount: 0, siteCount: 0`, with a logged warning) rather than failing the entire request — consistent with BR-12's "zero alerts is not an error" precedent. One informer's transient failure must not hide alert data for the other (up to 24) informers in the same response.
