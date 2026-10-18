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
