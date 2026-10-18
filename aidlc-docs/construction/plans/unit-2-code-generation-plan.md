# Code Generation Plan — Unit 2: Coordinator Agent (FR-6 Addendum: Informer Search & Alerting Service)

**Source of truth**: This plan, executed exactly as written. References: `functional-design/` (BR-12–BR-14, business-logic-model addendum) under `aidlc-docs/construction/unit-2/`. NFR Requirements/Design were skipped for this addendum (no new NFRs — see `execution-plan.md` Addendum: FR-6).

**Unit dependencies**: Depends on Unit 1's already-completed, approved `get_informer_status` tool contract (search/startDateTimeUtc/endDateTimeUtc in, `InformerStatusResult` JSON out with string `kind`). Unit 3 is unaffected.

**Workspace root for application code**: `C:\aem\ai-agentic\mcp` (brownfield — modifying existing `unit-2/src/CoordinatorAgent` and `unit-2/tests/CoordinatorAgent.Tests` in place).

## Steps

### Step 1 — Informer Parameter Extraction (Modify Existing)
- [x] Modify `unit-2/src/CoordinatorAgent/Services/ParameterExtractor.cs`:
  - Remove `InformerIdPattern` (`informer-[a-z0-9]+`) and `ZonePattern` (`zone-[a-z0-9]+`) regex fields — no longer used.
  - Add a new `InformerSearchPhrasePattern` regex: captures the phrase following the word "informer" up to a time-window qualifier (`between`, `last N <unit>`, `yesterday`, explicit `from X to Y` range) or end of string — same capture/lookahead style as `LocationPhrasePattern` (BR-12).
  - Rewrite `ExtractInformerParameters(string query)`: extract the search phrase; if not found, return `(false, null, "Missing required parameter: search. Please specify a customer name, informer name, MAC address, or informer ID (e.g., 'status of the informer Lakeside').")`. Otherwise call the existing `TryExtractTimeWindow(query)` (unchanged, BR-13) and convert non-null `DateTime` results to Unix epoch seconds (`new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeSeconds()` — `TryExtractTimeWindow` already returns UTC `DateTime`s) for `startDateTimeUtc`/`endDateTimeUtc`; omit both from the parameters object when `TryExtractTimeWindow` found no time phrase. Build `new { search, startDateTimeUtc, endDateTimeUtc }` and serialize via the existing `SerializeParameters` helper (which already drops null properties via `DefaultIgnoreCondition.WhenWritingNull`).

### Step 2 — Parameter Extraction Unit Testing (Modify Existing)
- [x] Modify `unit-2/tests/CoordinatorAgent.Tests/Services/ParameterExtractorTests.cs`: replace `ExtractInformerParameters_WithZone_ReturnsZone`/`ExtractInformerParameters_MissingBoth_ReturnsError` with tests covering: search phrase extracted from a plain query ("status of the informer Lakeside" → `search == "Lakeside"`), search phrase + explicit time range ("status of the informer Lakeside between 2026-01-01 and 2026-01-02" → `search` + both epoch params present and correctly converted), search phrase + "last N days"/"yesterday" phrasing (epoch params present), no time phrase at all (`startDateTimeUtc`/`endDateTimeUtc` both absent from the serialized JSON — confirms the "let Unit 1 default" behavior), and missing search text entirely (error message).

### Step 3 — Business Logic Summary
- [x] Create `aidlc-docs/construction/unit-2/code/informer-parameter-extraction-summary.md` documenting the `ParameterExtractor` changes (markdown summary only).

### Step 4 — Result Formatting (Modify Existing)
- [x] Modify `unit-2/src/CoordinatorAgent/Services/ResultFormatter.cs`: rewrite `BuildInformerSummary(JsonElement result)` per BR-14 — branch on `result.GetProperty("kind").GetString()`:
  - `"Found"`: read `match.informName`, count `alertSiteGroups[].alerts[].Length` summed across groups and the number of site groups; produce `"Informer '<informName>': <N> alert(s) across <M> site(s)."` or `"Informer '<informName>': no alerts in the requested time range."` when the total count is 0.
  - `"Disabled"`: `"Informer '<match.informName>' is disabled and not operational."`
  - `"Ambiguous"`: join `candidates[]` (each `informName`/`custName`/`macAddress`) into a list, e.g. `"Multiple informers match '<search>'. Did you mean: <candidate 1>; <candidate 2>? Please refine your search (e.g. by MAC address or informer ID)."` — the raw search text isn't in the tool response, so thread it through from the request in `QueryController` (see Step 6) or omit it from the message and just list candidates if threading it through adds unwarranted complexity; prefer listing candidates only, matching `BuildStrikeSummary`'s self-contained style (no other summary method references the original query text either).
  - `"NotFound"`: `"No informer found matching that search."` (also without echoing raw search text, for the same self-contained-summary reason).

### Step 5 — API Layer Summary
- [x] Create `aidlc-docs/construction/unit-2/code/informer-result-formatting-summary.md` documenting the `ResultFormatter.BuildInformerSummary` rewrite (markdown summary only).

### Step 6 — Integration Test Coverage (New)
- [x] Add informer-intent test cases to `unit-2/tests/CoordinatorAgent.Tests/Integration/CoordinatorIntegrationTests.cs` (or confirm/extend its existing fake-MCP-client test harness) covering: a full "status of the informer X" query round-trip through `QueryController` with a faked `get_informer_status` tool response for each of the 4 `kind` values, asserting the expected `summary` text.

### Step 7 — Documentation Generation
- [x] Create `aidlc-docs/construction/unit-2/code/code-generation-summary.md` — overall summary of all files created/modified for this addendum, cross-referencing Steps 1–6.

### Step 8 — Deployment Artifacts
- [x] None required — no new environment variables, no Dockerfile/compose changes (Unit 2 makes no new external calls; it only changes what parameters it sends to the already-configured Unit 1 MCP tool).

## Requirement Traceability
- FR-6 (Informer Search & Alerting Service) in `aidlc-docs/inception/requirements/requirements.md` — Steps 1, 4, 6
- BR-12–BR-14 in `aidlc-docs/construction/unit-2/functional-design/business-rules.md` — Steps 1, 4

## Estimated Scope
8 steps; ~3 files touched (2 modified: `ParameterExtractor.cs`, `ResultFormatter.cs`; 1 modified/extended: `CoordinatorIntegrationTests.cs`) plus 1 modified test file (`ParameterExtractorTests.cs`), plus 3 markdown summaries.
