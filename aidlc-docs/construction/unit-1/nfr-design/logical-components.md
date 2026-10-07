# Logical Components: Unit 1 — Lightning MCP Server

## Overview

This document details the logical components that implement Unit 1's NFR design patterns. Each component corresponds to a logical layer or responsibility; implementation details (specific .NET classes, files) are determined during Code Generation phase.

---

## Component Architecture

```
┌─────────────────────────────────────────────────────────┐
│  HTTP Server (ASP.NET Core)                             │
│  - Listen on LISTEN_PORT (env var, default 8000)        │
│  - Route MCP requests to Tool Registry                  │
│  - Handle graceful shutdown (SIGTERM)                   │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│  MCP Tool Registry                                      │
│  - Discover 4 domain modules at startup                 │
│  - Route incoming MCP calls to correct module           │
│  - Validate MCP request format                          │
│  - Ensure response well-formedness                      │
└────────────────┬────────────────────────────────────────┘
                 │
    ┌────────────┼────────────┬────────────┐
    │            │            │            │
    ▼            ▼            ▼            ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Strike   │ │ Weather  │ │ Sensor   │ │ Informer │
│ Detection│ │ Forecast │ │Diagnostic│ │ Status   │
│ Module   │ │ Module   │ │ Module   │ │ Module   │
└────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘
     │            │            │            │
     ▼            ▼            ▼            ▼
┌─────────────────────────────────────────────────────────┐
│  Shared Services                                        │
│  - ILogger (structured logging)                         │
│  - IConfiguration (env vars)                            │
│  - IHealthCheckProvider                                 │
└─────────────────────────────────────────────────────────┘
```

---

## Component Specifications

### 1. HTTP Server (Transport & Protocol Binding)

**Responsibility**: Listen for incoming HTTP/SSE requests, route to MCP Tool Registry, handle lifecycle.

**Key Features**:
- **Listen Port**: Read from `LISTEN_PORT` environment variable; default 8000.
- **Graceful Shutdown**:
  - On SIGTERM: Stop accepting new requests, wait up to 10 seconds for in-flight requests to complete, then exit.
  - Log shutdown event via structured logger.
- **Error Responses**: Return HTTP 400/500 with JSON error structure for invalid requests.
- **Health Endpoint**: `GET /health` returns `HTTP 200` with JSON body `{"status": "healthy", "timestamp": "2026-10-04T14:15:00Z"}`.

**Dependencies**:
- ASP.NET Core framework
- MCP SDK (HTTP/SSE binding)
- ILogger (for structured logging)

**Inputs**:
- HTTP POST requests from Coordinator Agent (or external MCP client)

**Outputs**:
- HTTP 200 + JSON MCP response (tool result)
- HTTP 400 + JSON error (invalid request)
- HTTP 500 + JSON error (internal error)
- HTTP 200 + JSON health status (`/health`)

---

### 2. MCP Tool Registry

**Responsibility**: Discover domain modules, register them as MCP tools, route incoming MCP calls to the correct module, validate inputs/outputs.

**Key Features**:
- **Startup Registration**: At application startup, discover all domain module classes (via reflection or explicit configuration), extract their MCP tool definitions (tool name, input schema, description), and register with the MCP SDK.
- **Routing**: When an MCP request arrives (e.g., `get_lightning_strikes_near_location` with parameters), match the tool name to the correct module and invoke it.
- **Input Validation**: Validate the MCP request structure (presence of required fields, correct JSON format) before passing to the module. Return HTTP 400 with error details if invalid.
- **Output Validation**: After the module returns a result, ensure the response JSON is well-formed (no NaN, Infinity, unescaped strings). Return HTTP 500 if validation fails (this should not happen with correct module code, but acts as a safety net).

**Dependencies**:
- MCP SDK
- ILogger
- Domain modules (injected)

**Inputs**:
- HTTP request body (MCP tool call with parameters)
- Domain modules (registered at startup)

**Outputs**:
- MCP response JSON (if successful)
- HTTP 400 error JSON (invalid input)
- HTTP 500 error JSON (validation failure)

---

### 3. Domain Modules (Strike Detection, Weather Forecast, Sensor Diagnostics, Informer Status)

Each module implements the same interface pattern and is responsible for one of the 4 MCP tools.

#### 3.1 Strike Detection Module (`get_lightning_strikes_near_location`)

**Responsibility**: Generate stubbed lightning strike/proximity data for a given location and radius.

**Input Validation**:
- `location` (required): Non-empty string (City+State or ZIP)
- `radius` (required): Positive number
- `radius_unit` (required): 'miles' or 'km'
- Return error if any required field is missing or invalid.

**Stub Data Generation** (deterministic):
- Seed a pseudo-random generator based on the input `location` and `radius` (e.g., `hashCode(location + radius)`).
- Generate N synthetic strike records (e.g., N = 3–7 based on seeded randomness).
- For each strike:
  - Random offset from queried location (within radius, seeded).
  - Random intensity/amplitude value (e.g., 1–50 kA, seeded).
  - Random timestamp within the last 24 hours (seeded, consistent with current time).
  - Calculated distance from location (based on offset).
- Compute nearest_strike_distance (minimum of all distances).

**Output**:
```json
{
  "location": "Austin, TX",
  "radius": 10,
  "radius_unit": "miles",
  "strike_count": 5,
  "nearest_strike_distance": 2.3,
  "strikes": [
    {
      "id": "strike-001",
      "latitude": 30.267153,
      "longitude": -97.743057,
      "distance_miles": 2.3,
      "timestamp": "2026-10-04T12:00:00Z",
      "intensity_kA": 25.5
    },
    ...
  ]
}
```

---

#### 3.2 Weather Forecast Module (`get_weather_forecast`)

**Responsibility**: Generate stubbed weather forecast data for a given location.

**Input Validation**:
- `location` (required): Non-empty string (City+State)
- `forecast_type` (required): 'daily' or '15-day'
- Return error if any required field is missing or invalid.

**Stub Data Generation** (deterministic):
- Seed a pseudo-random generator based on the input `location` and `forecast_type`.
- If `forecast_type = 'daily'`: Generate 1 forecast entry for today.
- If `forecast_type = '15-day'`: Generate 15 entries, one per day starting today.
- For each entry:
  - Date (today + N days)
  - Condition (randomly selected from a fixed list: 'Clear', 'Cloudy', 'Rainy', 'Thunderstorm', etc., seeded)
  - High/low temperature (in Fahrenheit, seeded)
  - Precipitation probability (0–100%, seeded)
  - Lightning-risk indicator ('Low', 'Moderate', 'High', seeded, preferring 'Low' on most days)

**Output**:
```json
{
  "location": "Denver, CO",
  "forecast_type": "daily",
  "forecast": [
    {
      "date": "2026-10-04",
      "condition": "Sunny",
      "temp_high_f": 72,
      "temp_low_f": 58,
      "precipitation_probability": 5,
      "lightning_risk": "Low"
    }
  ]
}
```

---

#### 3.3 Sensor Diagnostics Module (`get_sensor_diagnostics`)

**Responsibility**: Generate stubbed health/status diagnostics for a lightning detection sensor.

**Input Validation**:
- `sensor_id` (required): Non-empty string
- Return error if missing or empty.

**Stub Data Generation** (deterministic):
- Seed a pseudo-random generator based on the `sensor_id`.
- Generate representative diagnostic attributes:
  - Detection Efficiency (%): Seeded value 85–99%
  - GPS Visibility: Seeded value (e.g., 'Locked', 'Searching', 'Unlocked')
  - Tracked Satellites: Seeded count 8–15
  - Noise Level (dB): Seeded value 20–35 dB
  - Sensor Uptime (%): Seeded value 98–100%
  - Last Calibration Date: Seeded date (recent, e.g., within last 30 days)
  - Signal-to-Noise Ratio: Seeded value 5–15 dB
  - Overall Status: Derived from above (Healthy if all metrics are good, Degraded if some are off, Offline if GPS is Unlocked)

**Output**:
```json
{
  "sensor_id": "LN-204",
  "detection_efficiency_percent": 92,
  "gps_visibility": "Locked",
  "tracked_satellites": 12,
  "noise_level_dB": 28,
  "uptime_percent": 99.5,
  "last_calibration_date": "2026-09-20",
  "snr_dB": 10.2,
  "status": "Healthy"
}
```

---

#### 3.4 Informer Status Module (`get_informer_status`)

**Responsibility**: Generate stubbed status for physical warning devices (strobes/horns).

**Input Validation**:
- At least one of `informer_id` or `zone` must be provided and non-empty.
- Return error if both are missing.

**Stub Data Generation** (deterministic):
- Seed a pseudo-random generator based on the provided `informer_id` or `zone`.
- Generate representative device status:
  - Status: Seeded selection from 'Active', 'Idle', 'Fault' (Fault rare, ~5% chance)
  - Last Activation Time: Seeded timestamp (recent, e.g., within last 7 days if status is Active, further back if Idle)
  - Battery/Power Status: Seeded value (e.g., 'On Mains', 'On Battery 87%', 'Low Battery')
  - Zone/Location: Seeded zone name (e.g., 'Zone 3', 'North Gate', etc.)
  - Device Type: Seeded selection 'Strobe', 'Horn', 'Combined'

**Output**:
```json
{
  "informer_id": "DEV-502",
  "zone": "Zone 3",
  "status": "Active",
  "last_activation_time": "2026-10-03T18:45:00Z",
  "power_status": "On Mains",
  "device_type": "Strobe"
}
```

---

### 4. Shared Services Layer

**Responsibility**: Provide cross-cutting utilities to all components.

#### 4.1 Structured Logging Service (`ILogger`)

**Pattern**: ASP.NET Core's built-in `ILogger<T>` interface, configured with structured JSON formatter.

**Behavior**:
- All components inject `ILogger<T>` and log significant events.
- Log entries include: timestamp (ISO 8601), log level, message, optional contextual fields (tool name, input summary, elapsed time).
- Configuration:
  - Log level: Read from `LOG_LEVEL` environment variable (default: INFO)
  - Format: Structured JSON (one JSON object per line, stdout)
  - Sink: Console (stdout), which Docker captures in container logs

**Example Log Entry**:
```json
{
  "timestamp": "2026-10-04T14:15:00Z",
  "level": "Information",
  "logger": "LightningMCP.StrikeDetectionModule",
  "message": "Generating strikes",
  "tool": "get_lightning_strikes_near_location",
  "location": "Austin, TX",
  "radius": 10,
  "elapsed_ms": 45
}
```

**Logged Events**:
- Application startup and shutdown
- Each MCP tool invocation (input parameters, tool name)
- Each tool result (output size, result summary)
- Validation errors (missing input, invalid value)
- HTTP health check requests

---

#### 4.2 Configuration Service (`IConfiguration`)

**Pattern**: ASP.NET Core's `IConfiguration` builder, which reads from multiple sources in order: command-line args, environment variables, appsettings.json.

**Configured Values**:
- `LISTEN_PORT` (env var, default: 8000)
- `LOG_LEVEL` (env var, default: INFO)
- `MCP_PROTOCOL` (env var, default: http-sse; for future flexibility)

**Usage**: Components inject `IConfiguration` and read values at runtime.

---

#### 4.3 Health Check Provider (`IHealthCheckProvider`)

**Pattern**: Custom interface provided by LightningCommon (shared library).

**Behavior**:
- Implements a synchronous health check: Returns true if the service is healthy (all modules loaded, no critical errors).
- Called by the HTTP Server on `GET /health` request.
- Response: HTTP 200 + JSON `{"status": "healthy", "timestamp": "..."}`

---

## Component Interactions (Request Flow)

### Flow: Incoming MCP Tool Call

1. **HTTP Server** receives POST request on port 8000 with MCP tool call (e.g., `get_lightning_strikes_near_location`).
2. **HTTP Server** logs request received (structured JSON, INFO level).
3. **HTTP Server** routes request to **MCP Tool Registry**.
4. **MCP Tool Registry** validates MCP request format; if invalid, returns HTTP 400 error.
5. **MCP Tool Registry** matches tool name to domain module; invokes module with parsed inputs.
6. **Domain Module** (e.g., Strike Detection) validates inputs; if invalid, returns error object.
7. **Domain Module** generates stubbed response using deterministic seeding.
8. **Domain Module** returns response object to **MCP Tool Registry**.
9. **MCP Tool Registry** validates response JSON well-formedness; if invalid, returns HTTP 500 error.
10. **MCP Tool Registry** returns response to **HTTP Server**.
11. **HTTP Server** logs response returned (structured JSON, INFO level, including elapsed time).
12. **HTTP Server** sends HTTP 200 + JSON response to caller.

### Flow: Health Check

1. **HTTP Server** receives GET request to `/health`.
2. **HTTP Server** calls **Health Check Provider**.
3. **Health Check Provider** returns true (all components initialized).
4. **HTTP Server** returns HTTP 200 + JSON `{"status": "healthy", "timestamp": "..."}`

### Flow: Graceful Shutdown

1. Container receives SIGTERM signal.
2. **HTTP Server** is notified via `IHostApplicationLifetime.ApplicationStopping`.
3. **HTTP Server** stops accepting new connections.
4. **HTTP Server** waits up to 10 seconds for in-flight requests to complete.
5. **Shared Logging Service** logs shutdown event.
6. **HTTP Server** exits, process terminates.

---

## Implementation Notes for Code Generation Phase

- All components are unit-testable in isolation (dependency injection, mockable interfaces).
- Domain modules have no direct dependencies on HTTP layer; they can be tested by invoking methods directly.
- Shared Services Layer (ILogger, IConfiguration, IHealthCheckProvider) are provided by ASP.NET Core or LightningCommon; components do not implement them.
- No global state; all state is injected or derived from request inputs.
- All component interactions are synchronous (no async required for Phase 1); can be extended to async in Phase 2 if needed.

---

**Summary**: These logical components implement the NFR design patterns (fail-fast resilience, stateless scalability, deterministic performance, input/output security, and pluggable extensibility) while maintaining simplicity for a POC.

