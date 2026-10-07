# Domain Entities — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

## `GeocodeResult`
Represents a successfully resolved location.

| Field | Type | Description |
|---|---|---|
| `Latitude` | `double` | Resolved latitude |
| `Longitude` | `double` | Resolved longitude |
| `MatchedAddress` | `string` | The normalized/canonical address string returned by the Census Geocoder (echoed back to the user for confirmation, e.g. in logs or a future "resolved to: ..." response field) |

## `GeocodeCacheEntry`
Internal cache record (process-local, in-memory).

| Field | Type | Description |
|---|---|---|
| `Key` | `string` | Normalized cache key — lowercased + trimmed input location string (per Q3 answer: trim/lowercase only, no punctuation stripping) |
| `Result` | `GeocodeResult` | The cached resolution |
| `ExpiresAtUtc` | `DateTime` | TTL expiry (concrete TTL value set in NFR Design) |

## `GeocodeOutcome` (discriminated result — not a single success/failure bool)
Represents the three possible outcomes of a geocode attempt, consumed by `ParameterExtractor`.

| Case | Carries |
|---|---|
| `Resolved` | `GeocodeResult` |
| `Ambiguous` | `IReadOnlyList<string>` — list of candidate matched-address strings (e.g. `["Springfield, IL", "Springfield, MO", "Springfield, MA"]`) |
| `NotFound` | *(no payload)* |

This shape avoids overloading `GeocodeResult` with nullable ambiguous/not-found fields and makes each outcome's required data explicit at the type level.

## Relationship to Existing Types
- `ParameterExtractor.TryExtractLocation` (existing, currently returns `(double Latitude, double Longitude)?` backed by the hardcoded `KnownLocations` dictionary) is replaced by a call into the new `LocationGeocoder`, which returns a `GeocodeOutcome`.
- No changes to `Intent.cs` or to the MCP-facing request/response models — this is purely an internal Unit 2 resolution step that happens before building the `parameters` object passed to `_mcpClient.CallToolAsync`.
