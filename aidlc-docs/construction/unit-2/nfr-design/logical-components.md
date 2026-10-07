# Logical Components — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

## New Components

### `ILocationGeocoder` / `LocationGeocoder`
- **Location**: `unit-2/src/CoordinatorAgent/Services/ILocationGeocoder.cs`, `LocationGeocoder.cs`
- **Dependencies**: `HttpClient` (typed client for Census, via `AddHttpClient<ILocationGeocoder, LocationGeocoder>()`), `IHttpClientFactory` (*added 2026-10-06* — to resolve the named `"NominatimGeocoder"` client on demand), `IMemoryCache`, `IOptions<CensusGeocoderOptions>`, `IOptions<NominatimOptions>` (*added 2026-10-06*), `ILogger<LocationGeocoder>`
- **Public surface**: `Task<GeocodeOutcome> GeocodeAsync(string locationText, CancellationToken cancellationToken = default)`
- **Internal responsibilities**: cache-aside lookup, HTTP call to Census Geocoder with retry loop; **on zero Census matches**, HTTP call to Nominatim (fallback) with the same retry loop; response mapping (either provider) to `GeocodeOutcome`; cache write-back.

### `NominatimOptions` *(Added 2026-10-06)*
- **Location**: `unit-2/src/CoordinatorAgent/Services/NominatimOptions.cs`
- **Properties**: `string BaseUrl`, `int TimeoutSeconds`, `string UserAgent`

### `GeocodeOutcome` (+ `GeocodeResult`)
- **Location**: `unit-2/src/CoordinatorAgent/Models/GeocodeOutcome.cs` (or `Models/Geocoding.cs` grouping both types)
- A discriminated result type (e.g. a small class hierarchy or a record with a `Kind` enum: `Resolved | Ambiguous | NotFound`) per the Functional Design's `domain-entities.md`.

### `CensusGeocoderOptions`
- **Location**: `unit-2/src/CoordinatorAgent/Services/CensusGeocoderOptions.cs` (co-located with the client, mirroring `LightningPulseApiOptions`'s placement next to `LightningPulseApiClient` in Unit 1)
- **Properties**: `string BaseUrl`, `int TimeoutSeconds`

## Modified Components

### `ParameterExtractor` (`unit-2/src/CoordinatorAgent/Services/ParameterExtractor.cs`)
- Constructor gains `ILocationGeocoder` dependency.
- `TryExtractLocation` becomes **async** (`Task<(GeocodeOutcome Outcome, string? RawInput)>` or similar) since it now calls `GeocodeAsync` when no explicit lat/long pattern matches — the `KnownLocations` dictionary is deleted entirely.
- `ExtractStrikeParameters` and `ExtractWeatherParameters` (both currently synchronous) become async, awaiting the new `TryExtractLocation`, and branch on the returned `GeocodeOutcome` kind to produce the appropriate success/error tuple (per `business-rules.md` BR-4/BR-5 error message formats).
- Radius-cap check (100 miles) added inside `ExtractStrikeParameters` after `TryExtractRadius`, per `business-rules.md` BR-8.
- `IParameterExtractor.ExtractParameters` signature changes from synchronous to `Task<(bool, JsonElement?, string?)>` — its single caller, `QueryController.HandleQuery` (already `async`), updates its call site to `await`.

### `Program.cs` (`unit-2/src/CoordinatorAgent/Program.cs`)
- Add `builder.Services.AddMemoryCache();`
- Add `builder.Services.Configure<CensusGeocoderOptions>(...)` (env-var overrides, per NFR Design's Options pattern).
- Add `builder.Services.AddHttpClient<ILocationGeocoder, LocationGeocoder>();`
- *(Added 2026-10-06)* Add `builder.Services.Configure<NominatimOptions>(...)` (env-var overrides).
- *(Added 2026-10-06)* Add `builder.Services.AddHttpClient("NominatimGeocoder", client => { client.BaseAddress = ...; client.Timeout = ...; client.DefaultRequestHeaders.UserAgent.ParseAdd(...); });`

### `appsettings.json` (`unit-2/src/CoordinatorAgent/appsettings.json`)
- Add a `CensusGeocoder` section with `BaseUrl`/`TimeoutSeconds` (optional — defaults apply if omitted; included for discoverability/override, mirroring how Unit 1's `appsettings.json` likely documents `LightningPulseApi` settings).

## Component Interaction Flow (Strike intent example)
```
QueryController.HandleQuery
  -> ParameterExtractor.ExtractParameters (now async)
       -> ExtractStrikeParameters
            -> TryExtractLocation
                 -> (explicit lat/long in text? use directly, no geocoding)
                 -> else: ILocationGeocoder.GeocodeAsync(locationText)
                      -> IMemoryCache hit? return cached GeocodeOutcome
                      -> else: HttpClient call to Census Geocoder (up to 3 attempts, 500ms delay)
                                -> map response -> GeocodeOutcome -> cache it -> return
            -> branch on GeocodeOutcome: Resolved (continue) / Ambiguous (return error) / NotFound (return error)
            -> TryExtractRadius -> check 100-mile cap -> error if exceeded
       -> returns (success, parameters, error) as before
  -> unchanged: call MCP tool / return BadRequest
```

No changes to Unit 1 (`LightningMcpServer`) or Unit 3 (Infrastructure Orchestration) logical components.
