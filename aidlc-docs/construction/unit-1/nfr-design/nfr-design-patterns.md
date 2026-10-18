# NFR Design Patterns — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

## Resilience Pattern: Manual Retry Loop (live mode only)
- Both `InformerApiClient` (informer list) and `AlertsApiClient` perform up to 3 total attempts on `HttpRequestException`/`TaskCanceledException` (timeout), with a fixed 500ms `Task.Delay` between attempts — identical loop shape to `LocationGeocoder` (Unit 2, FR-4). No new dependency (Polly) introduced.
  - **Correction (2026-10-17, found during Code Generation planning)**: the NFR Requirements doc's claim that this "matches ... LightningPulseApiClient"/is "consistent with LightningPulseApiClient's existing resilience approach" is **inaccurate** — inspecting the actual code (`unit-1/src/LightningMcpServer/ExternalApis/LightningPulseApiClient.cs`, `WeatherForecastApiClient.cs`) shows neither Unit 1 HTTP client has any retry logic today; both make a single attempt and throw on failure. The retry-loop *design decision itself* is unaffected (it is independently justified by mirroring `LocationGeocoder`, Unit 2/FR-4) — only the precedent citation was wrong, now corrected here.
- `stub`-mode file reads have no retry loop — a missing/malformed stub file is a configuration error, surfaced immediately (not retried).
- After the 3rd failed live-mode attempt, the failure propagates as an exception; `InformerDirectory` catches it and the MCP tool layer surfaces a clear service-unavailable-style error — same pattern as `WeatherForecastModule`'s existing upstream-failure handling.

## Mode-Switch Pattern: Explicit Stub/Live Branch
- `InformerApiClient`/`AlertsApiClient` each check their own `DataSource` option (`stub`|`live`) at the start of their fetch method and branch to either the HTTP path or the file-read path — no shared abstraction/interface hides this distinction, since the two paths have genuinely different failure modes (HTTP retry/timeout vs. file-not-found) that should not be conflated behind one generic "fetch" interface.

## Performance Pattern: Cache-Aside (informer list only)
- `IMemoryCache.TryGetValue` checked first under a single fixed key (e.g. `"informer-list"`); on miss, `InformerApiClient.GetInformersAsync()` is called (live or stub per mode), and the result is written back (`Set` with `AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)`) before returning — same cache-aside shape as `LocationGeocoder`.
- No caching layer for `AlertsApiClient` — each `InformerDirectory` call needing alerts makes a fresh call (per NFR Requirements: time range varies per query, alert state changes frequently).

## Configuration Pattern: Options
- `InformerApiOptions` (`BaseUrl`, `TimeoutSeconds`, `DataSource`, `StubFilePath`) bound via `IOptions<InformerApiOptions>`, registered in `Program.cs` with env-var overrides — mirrors `LightningPulseApiOptions`/`WeatherForecastApiOptions` structurally.
  - Defaults: `BaseUrl = "http://qa.oas.api.enqa.co"`, `TimeoutSeconds = 10`, `DataSource = "stub"`, `StubFilePath = "data/informer/informer_list.json"`.
- `AlertsApiOptions` (`BaseUrl`, `TimeoutSeconds`, `DataSource`, `StubFilePath`), same pattern.
  - Defaults: `BaseUrl = "http://qa.alerts.api.enqa.co"`, `TimeoutSeconds = 10`, `DataSource = "stub"`, `StubFilePath = "data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json"`.
- Defaulting both `DataSource` values to `"stub"` matches the project's current reality (real endpoints unavailable off business hours) while keeping `live` a one-env-var switch away — no code change needed to go live later.

## Dependency Injection Pattern: Interface + Named HTTP Clients
- `IInformerApiClient`/`InformerApiClient` registered via `builder.Services.AddHttpClient<IInformerApiClient, InformerApiClient>()` (typed client, live-mode `HttpClient` injected directly) — same registration form as `ILightningPulseApiClient`/`IWeatherForecastApiClient`.
- `IAlertsApiClient`/`AlertsApiClient` registered the same way, as a second independent typed client (different base URL/timeout).
- `InformerDirectory` (replacing `InformerStatusModule`) takes `IInformerApiClient`, `IAlertsApiClient`, `IMemoryCache`, and `ILogger<InformerDirectory>` as constructor dependencies — registered via `builder.Services.AddScoped<IInformerStatusModule, InformerDirectory>()` (interface name `IInformerStatusModule` is kept since the MCP tool wiring in `Mcp`/`McpTools.cs` resolves against it; only the implementation class changes, per BR-13).

## Security
- Neither the Informer-list API nor the Alerts API requires an API key (matches `WeatherForecastApiClient`'s unauthenticated pattern) — no secret management concerns introduced for `live` mode.
- Stub files under `data/informer/` contain real-looking customer emails and `pasUserSecreteKey` GUIDs (QA data) — per user decision (2026-10-16), these are committed as-is to the repo; `InformerDirectory` does not log or re-expose `pasUserSecreteKey` or `custEmails` in any tool response (BR-10/BR-14 field-trimming already excludes them structurally, since `AlertRecord`/`InformerSearchMatch` do not carry those fields).
