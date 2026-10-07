# Intent & Parameter Reference: Unit 2 — Coordinator Agent

## Intent Keywords (checked in this order — first match wins)

| Intent | Keywords (case-insensitive, word boundary) |
|---|---|
| **Strike** | `strike`, `lightning`, `near`, `location` |
| **Weather** | `forecast`, `weather`, `temperature`, `rain`, `precipitation` |
| **Sensor** | `sensor`, `diagnostic`, `efficiency`, `calibration` |
| **Informer** | `informer`, `device`, `status`, `power`, `horn`, `strobe` |

If no keyword matches, the query is rejected with HTTP 400: *"Unable to determine what you're asking for..."*

## Known Locations (city-name lookup)

`austin`, `dallas`, `houston`, `san antonio`, `new york`, `miami`, `chicago`, `los angeles`, `seattle`, `denver` — matched as a case-insensitive substring anywhere in the query (e.g., "Austin, TX" and "Austin, Texas" both match `austin`).

Explicit coordinates are also accepted: `latitude 30.27, longitude -97.74` (regex: `lat(itude)? ... long(itude)? ...`).

## Parameter Extraction by Intent

### Strike
- **location** (required) — known city or explicit lat/long; error if neither found
- **radius** (optional, default `50`) — `\d+(\.\d+)? (km|kilometers|mi|miles)`
- **radiusUnit** (optional, default `km`)

### Weather
- **location** (required) — same as Strike
- **forecastType** (optional, default `daily`) — matches `daily` or `15-day`/`15 day`

### Sensor
- **sensorId** (required) — matches `sensor-<alphanumeric>` or `<2 letters>-<digits>` (e.g., `TX-123`); error if missing

### Informer
- **informerId** (optional) — matches `informer-<alphanumeric>`
- **zone** (optional) — matches `zone-<alphanumeric>` or `zone <alphanumeric>` (full token, e.g., `zone-a`)
- At least one of `informerId` / `zone` is required; error if both missing

## Example Query → Parameter Mappings

| Query | Intent | Parameters |
|---|---|---|
| "Is there lightning near Austin, TX?" | Strike | `{latitude: 30.2672, longitude: -97.7431, radius: 50, radiusUnit: "km"}` |
| "Is there lightning near Austin, TX within 30 miles?" | Strike | `{latitude: 30.2672, longitude: -97.7431, radius: 30, radiusUnit: "miles"}` |
| "What's the weather in Dallas?" | Weather | `{latitude: 32.7767, longitude: -96.797, forecastType: "daily"}` |
| "Give me the 15-day forecast for Dallas" | Weather | `{latitude: 32.7767, longitude: -96.797, forecastType: "15-day"}` |
| "Show diagnostics for sensor-001" | Sensor | `{sensorId: "sensor-001"}` |
| "Is the horn in zone-a powered on?" | Informer | `{zone: "zone-a"}` |
| "Is there lightning?" | Strike | **Error**: missing location |
| "Show me sensor diagnostics" | Sensor | **Error**: missing sensor_id |
| "What is the device status?" | Informer | **Error**: missing informer_id or zone |

## MCP Tool Invocation Mapping

| Intent | MCP Tool Invoked (Unit 1) |
|---|---|
| Strike | `get_lightning_strikes_near_location` |
| Weather | `get_weather_forecast` |
| Sensor | `get_sensor_diagnostics` |
| Informer | `get_informer_status` |

See `aidlc-docs/construction/unit-1/code/mcp-tool-schemas.md` for the exact tool input/output schemas.
