# Logical Components — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

## New Components

### `IInformerApiClient` / `InformerApiClient`
- **Location**: `unit-1/src/LightningMcpServer/ExternalApis/IInformerApiClient.cs`, `InformerApiClient.cs`
- **Dependencies**: `HttpClient` (typed client, live mode), `IOptions<InformerApiOptions>`, `ILogger<InformerApiClient>`
- **Public surface**: `Task<IReadOnlyList<InformerRecord>> GetInformersAsync(CancellationToken cancellationToken = default)`
- **Internal responsibilities**: branch on `DataSource` (live HTTP call with retry loop vs. stub-file read + deserialize); no caching here (caching lives in `InformerDirectory`, same separation as `LocationGeocoder` owning its own cache rather than pushing it into the HTTP client).

### `InformerApiOptions`
- **Location**: `unit-1/src/LightningMcpServer/ExternalApis/InformerApiOptions.cs`
- **Properties**: `string BaseUrl`, `int TimeoutSeconds`, `string DataSource`, `string StubFilePath`

### `IAlertsApiClient` / `AlertsApiClient`
- **Location**: `unit-1/src/LightningMcpServer/ExternalApis/IAlertsApiClient.cs`, `AlertsApiClient.cs`
- **Dependencies**: `HttpClient` (typed client, live mode), `IOptions<AlertsApiOptions>`, `ILogger<AlertsApiClient>`
- **Public surface**: `Task<IReadOnlyList<AlertSiteGroup>> GetAlertsAsync(string pasUserId, long startDateTimeUtc, long endDateTimeUtc, CancellationToken cancellationToken = default)` — returns already mapped/trimmed `AlertSiteGroup`s (BR-9/BR-10), not the raw upstream shape.
- **Internal responsibilities**: branch on `DataSource` (live HTTP call with retry loop + query-string construction vs. stub-file read + deserialize); map upstream `r[].nm`/`r[].a[]` to `AlertSiteGroup`/`AlertRecord`, filtering/trimming fields per BR-10.

### `AlertsApiOptions`
- **Location**: `unit-1/src/LightningMcpServer/ExternalApis/AlertsApiOptions.cs`
- **Properties**: `string BaseUrl`, `int TimeoutSeconds`, `string DataSource`, `string StubFilePath`

### `InformerDirectory` (replaces `InformerStatusModule`)
- **Location**: `unit-1/src/LightningMcpServer/DomainModules/InformerStatusModule.cs` (file/class renamed or content replaced in place — interface name `IInformerStatusModule` is retained)
- **Dependencies**: `IInformerApiClient`, `IAlertsApiClient`, `IMemoryCache`, `ILogger<InformerDirectory>`
- **Public surface**: `InformerStatusResult GetInformerStatus(GetInformerStatusRequest request)` — kept **synchronous**, matching `IInformerStatusModule`'s existing contract and the sibling convention already established by `StrikeDetectionModule`/`WeatherForecastModule` (both expose sync methods that block internally via `.GetAwaiter().GetResult()` on their async HTTP clients — confirmed by reading `StrikeDetectionModule.cs`). `GetInformerStatusRequest` replaces the old `InformerId`/`Zone` fields with `Search` (string) + optional `StartDateTimeUtc`/`EndDateTimeUtc` (long, epoch seconds).
- **Internal responsibilities**: cache-aside informer-list retrieval (BR-1/BR-2, blocking on `IInformerApiClient.GetInformersAsync()`), search matching (BR-3/BR-4), branch on match count (BR-6/BR-7), disabled short-circuit (BR-5), default time range (BR-11), delegate to `IAlertsApiClient.GetAlertsAsync()` (BR-8, also blocked on), assemble `InformerStatusResult` (BR-12).

### `InformerRecord`, `InformerSearchMatch`, `InformerSearchOutcome`, `AlertRecord`, `AlertSiteGroup`, `InformerStatusResult`
- **Location**: `unit-1/src/LightningMcpServer/Models/Informer.cs` (or split across a few files in `Models/`, grouping the types per `functional-design/domain-entities.md`)
- Plain data/discriminated-result types per the Functional Design — no behavior beyond construction/mapping.

## Removed Components
- `InformerStatus`, `GetInformerStatusRequest` (the old random-stub request/response models) — deleted, not kept alongside the new contract (BR-13).

## Modified Components

### `Program.cs` (`unit-1/src/LightningMcpServer/Program.cs`)
- Add `builder.Services.Configure<InformerApiOptions>(...)` (env-var overrides: `INFORMER_API_BASE_URL`, `INFORMER_API_TIMEOUT_SECONDS`, `INFORMER_DATA_SOURCE`, `INFORMER_STUB_FILE_PATH`).
- Add `builder.Services.AddHttpClient<IInformerApiClient, InformerApiClient>();`
- Add `builder.Services.Configure<AlertsApiOptions>(...)` (env-var overrides: `ALERTS_API_BASE_URL`, `ALERTS_API_TIMEOUT_SECONDS`, `ALERTS_DATA_SOURCE`, `ALERTS_STUB_FILE_PATH`).
- Add `builder.Services.AddHttpClient<IAlertsApiClient, AlertsApiClient>();`
- `builder.Services.AddScoped<IInformerStatusModule, InformerStatusModule>()` (existing line 69) changes only if the class is renamed to `InformerDirectory`; otherwise unchanged (interface→implementation mapping stays the same line, just a different class body).
- `IMemoryCache` is already registered for Unit 1 if any existing module uses it — confirm during Code Generation; if not, add `builder.Services.AddMemoryCache();` (idempotent if already present).

### MCP tool registration (`unit-1/src/LightningMcpServer/Mcp/` — exact file TBD during Code Generation, e.g. `McpTools.cs`/`ToolRegistry.cs`)
- The `get_informer_status` tool's input schema changes from `{ informer_id?, zone? }` to `{ search: string, startDateTimeUtc?: long, endDateTimeUtc?: long }` (BR-13) — schema/description update only, tool name unchanged.

### `appsettings.json` (`unit-1/src/LightningMcpServer/appsettings.json`)
- Add `InformerApi` and `AlertsApi` sections (optional — defaults apply if omitted), mirroring how `LightningPulseApi`/`WeatherForecastApi` are likely already documented there.

### `docker-compose.yml` / `.env`
- Add the 8 new env vars (4 per endpoint: base URL, timeout, data source, stub file path) to `lightning-mcp-server`'s `environment:`/`.env`, following the existing pattern for `LIGHTNING_PULSE_API_BASE_URL`/`WEATHER_FORECAST_API_BASE_URL`.
- No new volume mount needed — `data/` is already within the Docker build context (repo root), so stub files are available via a `COPY` in `unit-1/src/LightningMcpServer/Dockerfile` (confirm during Code Generation whether `data/` is already copied, or needs adding).

## Component Interaction Flow (informer-alert query example)
```
Unit 2: QueryController.HandleQuery
  -> (new Informer-alert intent) ParameterExtractor extracts search text + resolves
     local->UTC start/end (existing FR-1.1 conversion) or omits them entirely
  -> McpClient.CallToolAsync("get_informer_status", { search, startDateTimeUtc?, endDateTimeUtc? })

Unit 1: get_informer_status tool handler
  -> InformerDirectory.GetInformerStatusAsync(search, start?, end?)
       -> IMemoryCache hit (informer list)? use cached list : IInformerApiClient.GetInformersAsync()
            (live: HTTP w/ retry; stub: read data/informer/informer_list.json) -> cache it
       -> search match (substring, 4 fields, incl. disabled)
            -> 0 matches -> InformerStatusResult.NotFound
            -> >1 matches -> InformerStatusResult.Ambiguous(candidates)
            -> 1 match, IsDisabled -> InformerStatusResult.Disabled(match)
            -> 1 match, enabled:
                 -> resolve time range (caller values or default last 24h)
                 -> IAlertsApiClient.GetAlertsAsync(pasUserId, start, end)
                      (live: HTTP w/ retry; stub: read data/informer/salesforce_oas_....json)
                      -> map/trim to AlertSiteGroup[]
                 -> InformerStatusResult.Found(match, alertSiteGroups)
  -> tool returns mapped JSON per BR-14 error convention for non-Found cases

Unit 2: QueryController formats InformerStatusResult JSON into the chat-facing summary
  (ResultFormatter — updated in Unit 2 Code Generation)
```

No changes to Unit 3 (Infrastructure Orchestration) logical components.
