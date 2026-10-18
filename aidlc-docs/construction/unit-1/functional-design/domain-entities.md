# Domain Entities — Unit 1 (LightningMcpServer) — FR-6 Informer Search & Alerting Service

## `InformerRecord`
One row from the informer list source (real or stub), mapped directly from the upstream/stub JSON shape.

| Field | Type | Description |
|---|---|---|
| `CustId` | `int` | Customer ID |
| `InformId` | `int` | Informer ID |
| `MacAddress` | `string` | Device MAC address |
| `InformName` | `string` | Informer/device name |
| `PasUserId` | `string` | Key used to call the Alerts API (`userId` query param) |
| `IsDisabled` | `bool` | Whether the device is disabled |
| `CustName` | `string` | Customer/site name |

*(Other upstream fields — `pasUserSecreteKey`, `salesForceId`, `latitude`/`longitude`/`radius`/`duration`, `custEmails`, `targetFirmwareVersion*`, `isCgOnly`, `manualAllClear` — are deserialized but not used by this feature; not modeled as separate entities.)*

## `InformerSearchMatch`
A single informer surfaced as a search result or disambiguation candidate (subset of `InformerRecord`, enough to disambiguate).

| Field | Type | Description |
|---|---|---|
| `InformId` | `int` | |
| `InformName` | `string` | |
| `MacAddress` | `string` | |
| `CustName` | `string` | |
| `IsDisabled` | `bool` | |

## `InformerSearchOutcome` (discriminated result)
Result of Step 1 (search), consumed before deciding whether to call the Alerts API.

| Case | Carries |
|---|---|
| `SingleMatch` | `InformerSearchMatch` (+ `PasUserId` internally, not surfaced to caller) |
| `Ambiguous` | `IReadOnlyList<InformerSearchMatch>` — all matched candidates |
| `NotFound` | *(no payload — BR-9: no separate close-match suggestion)* |

## `AlertRecord`
One trimmed alert item (per Q6=A: all 5 UTC fields + `atn` + `m`; all other upstream alert fields dropped).

| Field | Type | Description |
|---|---|---|
| `Atn` | `string` | Alert type name (upstream `atn`) |
| `Message` | `string` | Alert message (upstream `m`) |
| `ActionUtc` | `DateTime?` | Upstream `actutc` |
| `IssuedUtc` | `DateTime?` | Upstream `issutc` |
| `ExpiresUtc` | `DateTime?` | Upstream `exputc` |
| `AutoClearUtc` | `DateTime?` | Upstream `acutc` |
| `LastModifiedUtc` | `DateTime?` | Upstream `lmutc` |

## `AlertSiteGroup`
Alerts grouped by site name (Q5=B — not flattened).

| Field | Type | Description |
|---|---|---|
| `SiteName` | `string` | Upstream `r[].nm` |
| `Alerts` | `IReadOnlyList<AlertRecord>` | Trimmed alerts for this site within the requested time range |

## `InformerStatusResult` (discriminated result — the new `get_informer_status` tool's output)

| Case | Carries |
|---|---|
| `Found` | `InformerSearchMatch` + `IReadOnlyList<AlertSiteGroup>` (may be empty — "no alerts found" case handled by an empty-list response, not a distinct error case) |
| `Disabled` | `InformerSearchMatch` — alerts lookup skipped entirely (BR-5) |
| `Ambiguous` | `IReadOnlyList<InformerSearchMatch>` |
| `NotFound` | *(no payload)* |

## Relationship to Existing Types
- Replaces `InformerStatus`/`GetInformerStatusRequest`/`IInformerStatusModule`/`InformerStatusModule` (currently random-stub) entirely — same MCP tool name (`get_informer_status`), new contract.
- `InformerSearchOutcome` and `InformerStatusResult` follow the same discriminated-result pattern as Unit 2's `GeocodeOutcome` (FR-4) — explicit cases instead of nullable/overloaded fields.
- New cache entry type (informer list, 1-hour TTL) follows the same `IMemoryCache` cache-aside pattern as `LocationGeocoder` (FR-4) and `WeatherForecastApiClient` (FR-5) — a raw `IReadOnlyList<InformerRecord>` cached under a single fixed key (no per-query keying, unlike the geocoder cache, since the whole list is fetched/cached as one unit).
