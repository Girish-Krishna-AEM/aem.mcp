# Informer Directory — Code Summary (FR-6, Unit 1)

## New files
- `unit-1/src/LightningMcpServer/ExternalApis/InformerApiOptions.cs` — config for the informer-list endpoint (base URL, timeout, stub/live switch, stub file path).
- `unit-1/src/LightningMcpServer/ExternalApis/AlertsApiOptions.cs` — same shape, for the alerts endpoint.
- `unit-1/src/LightningMcpServer/ExternalApis/IInformerApiClient.cs` / `InformerApiClient.cs` — fetches the informer list (live HTTP with 3-attempt/500ms retry, or stub-file read), no caching (caching lives in `InformerStatusModule`).
- `unit-1/src/LightningMcpServer/ExternalApis/IAlertsApiClient.cs` / `AlertsApiClient.cs` — fetches alerts by `pasUserId`/time range (live or stub), maps and trims the upstream `r[].nm`/`r[].a[]` shape down to `AlertSiteGroup`/`AlertRecord` (only `atn`/`m`/5 UTC fields survive).

## Modified files
- `unit-1/src/LightningCommon/DomainModels.cs` — removed the old random-stub `InformerStatus`/`GetInformerStatusRequest`; added `GetInformerStatusRequest` (new shape: `Search`, optional `StartDateTimeUtc`/`EndDateTimeUtc`), `InformerRecord`, `InformerSearchMatch`, `AlertRecord`, `AlertSiteGroup`, and the discriminated `InformerStatusResult` (`Found`/`Disabled`/`Ambiguous`/`NotFound`).
- `unit-1/src/LightningMcpServer/DomainModules/IInformerStatusModule.cs` — return type updated to `InformerStatusResult`.
- `unit-1/src/LightningMcpServer/DomainModules/InformerStatusModule.cs` — rewritten: cache-aside informer-list retrieval (1hr TTL), substring search across 4 fields (including disabled), branch on match count, disabled short-circuit, 24h default time range, delegates to `IAlertsApiClient`.

## Business rules implemented
BR-1 through BR-14 (`aidlc-docs/construction/unit-1/functional-design/business-rules.md`) — stub/live mode switch, 1hr cache, fuzzy search, disabled-informer handling, disambiguation, site-grouped/trimmed alerts, 24h default window, empty-alerts-is-not-an-error, tool-contract replacement.
