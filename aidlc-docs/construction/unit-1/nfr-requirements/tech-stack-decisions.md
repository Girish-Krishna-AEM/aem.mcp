# Tech Stack Decisions — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

No new runtime technology is introduced — this stays within the existing Unit 1 stack (.NET 10.0 / C#, ASP.NET Core), matching the existing `LightningPulseApiClient`/`WeatherForecastApiClient` pattern.

| Decision | Choice | Rationale |
|---|---|---|
| Informer-list source | Configurable `INFORMER_API_BASE_URL` (`live`) or local file `data/informer/informer_list.json` (`stub`), selected by `INFORMER_DATA_SOURCE` | Real endpoint unavailable off business hours; explicit switch avoids silent failover ambiguity (FR-6/Q1) |
| Alerts source | Configurable `ALERTS_API_BASE_URL` (`live`) or local file `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json` (`stub`), selected by `ALERTS_DATA_SOURCE` | Same rationale as above, mirrored for the second endpoint |
| HTTP client | `IHttpClientFactory` named/typed clients (built into ASP.NET Core — no new NuGet package) | Standard practice already used by `LightningPulseApiClient`/`WeatherForecastApiClient` |
| JSON file reads (`stub` mode) | `System.Text.Json` (`File.ReadAllTextAsync` + `JsonSerializer.Deserialize`) — already a direct dependency | No new dependency; consistent with existing JSON handling elsewhere in Unit 1 |
| Caching (informer list) | `Microsoft.Extensions.Caching.Memory.IMemoryCache` (built into ASP.NET Core), single fixed key, 1-hour TTL | No new infrastructure (e.g. Redis); matches `LocationGeocoder`'s cache-aside pattern |
| Search matching | Plain `string.Contains(..., StringComparison.OrdinalIgnoreCase)` across the 4 fields — no fuzzy-matching library | Requirements (Q2=C) call for substring matching, not edit-distance fuzziness; no new dependency needed |
| Testing | xUnit + existing mocking approach already used in `LightningMcpServer.Tests` (mocked `HttpMessageHandler`) plus direct stub-file-backed tests | Consistent with existing test project; stub files double as deterministic test fixtures |

**No new NuGet packages are required** for FR-6 — `IHttpClientFactory`, `IMemoryCache`, and `System.Text.Json` are all already referenced by `LightningMcpServer`.
