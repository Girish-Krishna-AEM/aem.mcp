# Code Generation Plan — Unit 1: Lightning MCP Server (FR-6 Addendum: Informer Search & Alerting Service)

**Source of truth**: This plan, executed exactly as written. References: `functional-design/`, `nfr-requirements/`, `nfr-design/` (patterns, logical components) under `aidlc-docs/construction/unit-1/`.

**Unit dependencies**: Unit 2 (`CoordinatorAgent`) depends on this tool's updated contract but is addressed in its own separate Code Generation pass (`unit-2-code-generation-plan.md`, re-opened after this unit). Unit 3 is unaffected.

**Workspace root for application code**: `C:\aem\ai-agentic\mcp` (brownfield — modifying existing `unit-1/src/LightningMcpServer`, `unit-1/src/LightningCommon`, and `unit-1/tests/LightningMcpServer.Tests` in place; also adding `data/informer/` references, already committed).

**Note on prior NFR docs**: during this planning pass, inspecting the actual code showed `LightningPulseApiClient`/`WeatherForecastApiClient` have **no existing retry logic or `TimeoutSeconds` config** — corrected in `nfr-requirements.md`/`nfr-design-patterns.md` (2026-10-17). This plan's retry loop and `TimeoutSeconds` options are net-new to Unit 1, borrowed from Unit 2's `LocationGeocoder` precedent, not from an existing Unit 1 pattern.

## Steps

### Step 1 — Domain Models (Modify Existing)
- [x] Modify `unit-1/src/LightningCommon/DomainModels.cs`:
  - Remove `InformerStatus` and `GetInformerStatusRequest` classes (lines ~146–159, random-stub shape).
  - Add `GetInformerStatusRequest` (new shape): `string Search`, `long? StartDateTimeUtc`, `long? EndDateTimeUtc`.
  - Add `InformerRecord`: `CustId` (int), `InformId` (int), `MacAddress` (string), `InformName` (string), `PasUserId` (string), `IsDisabled` (bool), `CustName` (string) — with `[JsonPropertyName]` attributes matching the upstream/stub JSON field names (`custId`, `informId`, `macAddress`, `informName`, `pasUserId`, `isDisabled`, `custName`); other upstream fields are not modeled (permissive deserialization).
  - Add `InformerSearchMatch`: `InformId`, `InformName`, `MacAddress`, `CustName`, `IsDisabled` (subset of `InformerRecord`, per `domain-entities.md`).
  - Add `AlertRecord`: `Atn`, `Message`, `ActionUtc`, `IssuedUtc`, `ExpiresUtc`, `AutoClearUtc`, `LastModifiedUtc` (nullable `DateTime?` for the timestamp fields).
  - Add `AlertSiteGroup`: `SiteName` (string), `Alerts` (`IReadOnlyList<AlertRecord>`).
  - Add `InformerStatusResult` (discriminated result, `Kind` enum `Found`/`Disabled`/`Ambiguous`/`NotFound` + private constructor + static factory methods `Found(InformerSearchMatch, IReadOnlyList<AlertSiteGroup>)`, `Disabled(InformerSearchMatch)`, `Ambiguous(IReadOnlyList<InformerSearchMatch>)`, `NotFound`) — same pattern as Unit 2's `GeocodeOutcome` (`unit-2/src/CoordinatorAgent/Models/GeocodeOutcome.cs`).

### Step 2 — Configuration (New Files)
- [x] Create `unit-1/src/LightningMcpServer/ExternalApis/InformerApiOptions.cs`: `BaseUrl` (default `"http://qa.oas.api.enqa.co"`), `TimeoutSeconds` (default `10`), `DataSource` (default `"stub"`), `StubFilePath` (default `"data/informer/informer_list.json"`).
- [x] Create `unit-1/src/LightningMcpServer/ExternalApis/AlertsApiOptions.cs`: `BaseUrl` (default `"http://qa.alerts.api.enqa.co"`), `TimeoutSeconds` (default `10`), `DataSource` (default `"stub"`), `StubFilePath` (default `"data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json"`).

### Step 3 — Informer List Client (New Files)
- [x] Create `unit-1/src/LightningMcpServer/ExternalApis/IInformerApiClient.cs`: `Task<IReadOnlyList<InformerRecord>> GetInformersAsync(CancellationToken cancellationToken = default)`.
- [x] Create `unit-1/src/LightningMcpServer/ExternalApis/InformerApiClient.cs`: branches on `_options.DataSource` — `"live"`: calls `GET {BaseUrl}/api/oas/Informer/v1/GetInformers` via typed `HttpClient`, 3 total attempts with fixed 500ms `Task.Delay` on `HttpRequestException`/`TaskCanceledException` (BR-1, NFR Design resilience pattern), deserializes the JSON array to `List<InformerRecord>`; `"stub"` (any other value defaults to stub): reads and deserializes `StubFilePath` from disk (`File.ReadAllTextAsync` + `JsonSerializer.Deserialize<List<InformerRecord>>`). Throws a new `InformerApiException` (mirrors `LightningPulseApiException`) on failure after retries exhausted or on file-read error.

### Step 4 — Alerts Client (New Files)
- [x] Create `unit-1/src/LightningMcpServer/ExternalApis/IAlertsApiClient.cs`: `Task<IReadOnlyList<AlertSiteGroup>> GetAlertsAsync(string pasUserId, long startDateTimeUtc, long endDateTimeUtc, CancellationToken cancellationToken = default)`.
- [x] Create `unit-1/src/LightningMcpServer/ExternalApis/AlertsApiClient.cs`: branches on `_options.DataSource` — `"live"`: calls `GET {BaseUrl}/api/alerts/v1/ByUserIdAndTimeRange?startDateTimeUtc={start}&enddatetimeutc={end}&compact=true&includeUserAuditEvents=true&userId={pasUserId}` via typed `HttpClient`, same 3-attempt/500ms retry as Step 3; `"stub"`: reads/deserializes `StubFilePath`. Both paths parse the upstream `{ "r": [ { "nm": ..., "a": [ { "atn", "m", "actutc", "issutc", "exputc", "acutc", "lmutc", ... } ] } ] }` shape and map to `IReadOnlyList<AlertSiteGroup>`, trimming each alert to exactly `AlertRecord`'s 7 fields (BR-10) — all other upstream alert fields (`uaid`/`id`/`apid`/`read`/`latest`/`uril`/`uat`/`trig`/`mam`) are dropped during mapping, never deserialized into a public type. Throws `AlertsApiException` (same pattern as Step 3) on failure.

### Step 5 — InformerDirectory (Modify Existing, Replaces InformerStatusModule)
- [x] Modify `unit-1/src/LightningMcpServer/DomainModules/IInformerStatusModule.cs`: change method signature to `InformerStatusResult GetInformerStatus(GetInformerStatusRequest request)` (return type and request shape change; method name/interface name unchanged per BR-13).
- [x] Modify `unit-1/src/LightningMcpServer/DomainModules/InformerStatusModule.cs` (rewrite body in place, class name may become `InformerDirectory` internally while the file keeps serving the `IInformerStatusModule` DI mapping):
  - Remove the random-`Random`-seeded stub logic entirely.
  - Constructor dependencies: `IInformerApiClient`, `IAlertsApiClient`, `IMemoryCache`, `ILogger<InformerStatusModule>` (or `ILogger<InformerDirectory>` if renamed).
  - `GetInformerStatus(request)` — **synchronous** method (matches existing `IInformerStatusModule`/`IStrikeDetectionModule` convention; internally blocks via `.GetAwaiter().GetResult()` on the async API-client calls, same pattern already used in `StrikeDetectionModule.cs` line ~43):
    1. Cache-aside informer list: `_cache.TryGetValue("informer-list", out IReadOnlyList<InformerRecord>? list)`; on miss, `_informerApiClient.GetInformersAsync().GetAwaiter().GetResult()`, then `_cache.Set("informer-list", list, TimeSpan.FromHours(1))` (BR-2).
    2. Search: case-insensitive substring match of `request.Search` against `CustName`/`InformName`/`MacAddress`/`InformId.ToString()` across the full list, including disabled records (BR-3/BR-4).
    3. 0 matches → `InformerStatusResult.NotFound` (BR-6).
    4. >1 matches → `InformerStatusResult.Ambiguous(candidates)` where `candidates` are mapped to `InformerSearchMatch` (BR-7).
    5. Exactly 1 match, `IsDisabled == true` → `InformerStatusResult.Disabled(match)`, no Alerts call (BR-5).
    6. Exactly 1 match, enabled: resolve `start`/`end` — use `request.StartDateTimeUtc`/`EndDateTimeUtc` if both present, else default to `(DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeSeconds(), DateTimeOffset.UtcNow.ToUnixTimeSeconds())` (BR-11).
    7. Call `_alertsApiClient.GetAlertsAsync(match.PasUserId, start, end).GetAwaiter().GetResult()` (BR-8).
    8. Return `InformerStatusResult.Found(matchAsSearchMatch, alertSiteGroups)` — empty `alertSiteGroups` is valid, not an error (BR-12).
  - Logging per NFR Requirements Observability section (`Information` for cache hit/miss + branch taken, `Warning` for `Ambiguous`/`NotFound`/`Disabled`, `Error` on final failure).

### Step 6 — Business Logic Unit Testing (New + Modify)
- [x] Modify `unit-1/tests/LightningMcpServer.Tests/DomainModules/InformerStatusModuleTests.cs`: replace the random-stub tests with mocked `IInformerApiClient`/`IAlertsApiClient` (Moq) covering: single enabled match with alerts (`Found`), single enabled match with zero alerts in range (`Found`, empty groups), single disabled match (`Disabled`, confirms `IAlertsApiClient` is never called), zero matches (`NotFound`), multiple matches (`Ambiguous` with correct candidate list), default-24h time range applied when both start/end omitted, and informer-list cache-hit skips a second `GetInformersAsync()` call on a repeated `GetInformerStatus` call within the same `InformerDirectory` instance.
- [x] Create `unit-1/tests/LightningMcpServer.Tests/ExternalApis/InformerApiClientTests.cs`: mocked `HttpMessageHandler` for `live` mode (success, timeout-then-retry-succeeds, timeout-exhausts-3-attempts) + a stub-mode test reading the real `data/informer/informer_list.json` file (parses successfully, returns expected count/shape) — same dual convention as `LightningPulseApiClientTests.cs` (HTTP mocking) plus a new stub-file-backed case.
- [x] Create `unit-1/tests/LightningMcpServer.Tests/ExternalApis/AlertsApiClientTests.cs`: same dual convention — mocked-HTTP `live`-mode tests (success, retry, exhaustion) + a stub-mode test reading `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json`, asserting the site-grouping and field-trimming (only `atn`/`m`/5 UTC fields present on each mapped `AlertRecord`).

### Step 7 — Business Logic Summary
- [x] Create `aidlc-docs/construction/unit-1/code/informer-directory-summary.md` documenting the new/modified files from Steps 1–6 and their responsibilities (markdown summary only).

### Step 8 — MCP Tool Contract Update (Modify Existing)
- [x] Modify `unit-1/src/LightningMcpServer/Mcp/McpTools.cs`: update the `get_informer_status` tool method — change parameters from `informerId`/`zone` to `search` (required string), `startDateTimeUtc`/`endDateTimeUtc` (optional `long?`), change return type to `InformerStatusResult`, update the `[Description]` text to reflect the new search+alerts behavior.
- [x] Modify `unit-1/src/LightningMcpServer/Mcp/ToolRegistry.cs`: update the `get_informer_status` entry in `ListTools()`'s `inputSchema` (properties: `search` required string, `startDateTimeUtc`/`endDateTimeUtc` optional number) and update `HandleInformerStatus(arguments)` to build the new `GetInformerStatusRequest` shape and call `_informerModule.GetInformerStatus(request)` (unchanged call site otherwise, since the method stays synchronous).

### Step 9 — API Layer Summary
- [x] Create `aidlc-docs/construction/unit-1/code/informer-tool-contract-update-summary.md` documenting the `McpTools.cs`/`ToolRegistry.cs` changes (markdown summary only).

### Step 10 — Dependency Injection Wiring (Modify Existing)
- [x] Modify `unit-1/src/LightningMcpServer/Program.cs`:
  - Add `builder.Services.AddMemoryCache();` (not currently registered in Unit 1 — new addition, needed for Step 5's cache-aside).
  - Add `builder.Services.Configure<InformerApiOptions>(...)` (env-var overrides) + `builder.Services.AddHttpClient<IInformerApiClient, InformerApiClient>();`
  - Add `builder.Services.Configure<AlertsApiOptions>(...)` (env-var overrides) + `builder.Services.AddHttpClient<IAlertsApiClient, AlertsApiClient>();`
  - Line 69 (`builder.Services.AddScoped<IInformerStatusModule, InformerStatusModule>();`) stays unchanged (same interface→implementation class name, body changed in Step 5).

### Step 11 — Deployment Artifacts (Modify Existing)
- [x] Modify `.env` / `.env.example` (whichever exists as the committed template — confirm during execution): add `INFORMER_API_BASE_URL`, `INFORMER_API_TIMEOUT_SECONDS`, `INFORMER_DATA_SOURCE`, `INFORMER_STUB_FILE_PATH`, `ALERTS_API_BASE_URL`, `ALERTS_API_TIMEOUT_SECONDS`, `ALERTS_DATA_SOURCE`, `ALERTS_STUB_FILE_PATH` (all defaulting to `stub` mode with the committed stub file paths, so the stack runs correctly out of the box).
- [x] Modify `docker-compose.yml`: add the 8 new env vars to `lightning-mcp-server`'s `environment:` block (with `${VAR:-default}` fallback syntax matching the existing `LIGHTNING_PULSE_API_BASE_URL` style).
- [x] Verify `unit-1/src/LightningMcpServer/Dockerfile`'s build context already includes `data/` — **it did not** (confirmed by reading the Dockerfile; it only `COPY`s `LightningCommon`/`LightningMcpServer` source and the published output, never `data/`). Added `COPY data/ ./data/` to the runtime stage so the stub files are present in the running container at `/app/data/...`, matching the default `StubFilePath` values.

### Step 12 — Documentation Generation
- [x] Create `aidlc-docs/construction/unit-1/code/code-generation-summary.md` — overall summary of all files created/modified for this addendum, cross-referencing Steps 1–11.

## Requirement Traceability
- FR-6 (Informer Search & Alerting Service) in `aidlc-docs/inception/requirements/requirements.md` — all steps
- BR-1–BR-14 in `aidlc-docs/construction/unit-1/functional-design/business-rules.md` — Steps 1, 3, 4, 5
- NFR Design (`nfr-design-patterns.md`, `logical-components.md`) — Steps 2, 3, 4, 5, 10

## Estimated Scope
12 steps; ~11 files touched (6 new: `InformerApiOptions.cs`, `AlertsApiOptions.cs`, `IInformerApiClient.cs`, `InformerApiClient.cs`, `IAlertsApiClient.cs`, `AlertsApiClient.cs`; 5 modified: `DomainModels.cs`, `IInformerStatusModule.cs`, `InformerStatusModule.cs`, `McpTools.cs`, `ToolRegistry.cs`, `Program.cs`) plus 1 modified + 2 new test files, plus 3 markdown summaries, plus `.env`/`docker-compose.yml`/`Dockerfile` touch-ups.
