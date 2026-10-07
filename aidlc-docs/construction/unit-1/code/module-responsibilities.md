# Module Responsibilities — Lightning MCP Server

## Architecture Overview

The Lightning MCP Server is organized into 4 logical layers:

```
┌─────────────────────────────────────────┐
│  HTTP Controllers Layer                 │
│  (McpController, HealthController)      │
├─────────────────────────────────────────┤
│  MCP Integration Layer                  │
│  (Tool Registry, Tool Definitions)      │
├─────────────────────────────────────────┤
│  Domain Modules Layer                   │
│  (Strike, Weather, Sensor, Informer)    │
├─────────────────────────────────────────┤
│  Shared Services (Logger, Config, DI)   │
└─────────────────────────────────────────┘
```

---

## Layer 1: HTTP Controllers

### McpController
- **Responsibility**: Handle HTTP POST requests to `/mcp/messages`
- **Methods**:
  - `HandleMcpMessage(JsonElement request)` — Route to tools/list or tools/call
  - `HandleToolCall(JsonElement request)` — Parse tool invocation, call registry
- **Error Handling**: Returns structured error responses (400 Bad Request, 500 Internal Server Error)
- **Logging**: Logs incoming requests, tool names, and errors

### HealthController
- **Responsibility**: Handle HTTP GET requests to `/health`
- **Methods**:
  - `GetHealth()` — Return `{"status": "healthy", "timestamp": "..."}`
- **Purpose**: Liveness probe for orchestration (Kubernetes, Docker Compose)
- **Logging**: Logs health check requests

---

## Layer 2: MCP Integration

### ToolRegistry (IToolRegistry)

**Responsibility**: Maintain tool definitions and route invocations to domain modules.

**Methods**:
- `ListTools()` — Return JSON with all 4 tool definitions (name, description, input/output schemas)
- `CallTool(string toolName, JsonElement arguments)` — Route to correct domain module based on tool name

**Tool Definitions**:
1. `get_lightning_strikes_near_location` → StrikeDetectionModule
2. `get_weather_forecast` → WeatherForecastModule
3. `get_sensor_diagnostics` → SensorDiagnosticsModule
4. `get_informer_status` → InformerStatusModule

**Error Handling**:
- Throws `ArgumentException` for unknown tools
- Logs errors and re-throws for controller to handle

**Dependencies**:
- Injected instances of all 4 domain modules
- ILogger for structured logging

---

## Layer 3: Domain Modules

Each domain module encapsulates one business capability. All modules follow the same pattern:
1. Validate input
2. Generate deterministic stub data (seeded by input)
3. Return structured response
4. Log request and result

### Strike Detection Module

**Interface**: `IStrikeDetectionModule`

**Method**: `GetLightningStrikesNearLocation(GetLightningStrikesNearLocationRequest request)`

**Responsibility**: Return lightning strikes near a location.

**Input Validation**:
- Latitude/Longitude: Valid numbers (not NaN)
- Radius: Must be > 0
- RadiusUnit: Must be "km" or "miles"

**Deterministic Generation**:
- Seed: Hash of `"{latitude:F6}{longitude:F6}"`
- Output: 2-12 strikes (deterministic count per location)
- Each strike: Coordinates near request (offset by 1% of radius), intensity (1000-6000), timestamp

**Output**:
```csharp
{
  StrikeCount: int,           // 2-12
  Strikes: List<LightningStrike>,  // Strike array
  NearestStrikeDistance: double    // Calculated using Haversine formula
}
```

---

### Weather Forecast Module

**Interface**: `IWeatherForecastModule`

**Method**: `GetWeatherForecast(GetWeatherForecastRequest request)`

**Responsibility**: Return weather forecast with lightning risk.

**Input Validation**:
- Latitude/Longitude: Valid numbers (not NaN)
- ForecastType: Must be "daily" or "15-day"

**Deterministic Generation**:
- Seed: Hash of `"{latitude:F6}{longitude:F6}"`
- Conditions: Sunny, Cloudy, Rainy, Stormy, Partly Cloudy (deterministic per date)
- Metrics: Temperature (15-45°C), Precipitation (0-100%), Lightning Risk (0-100%)

**Output**:
```csharp
{
  Forecast: List<WeatherForecastEntry>  // 1 entry (daily) or 15 entries (15-day)
}

// Each entry:
{
  Date: string,                      // YYYY-MM-DD
  Condition: string,                 // Weather condition
  Temp: int,                        // Temperature in Celsius
  PrecipitationPercent: int,        // 0-100
  LightningRisk: int                // 0-100
}
```

---

### Sensor Diagnostics Module

**Interface**: `ISensorDiagnosticsModule`

**Method**: `GetSensorDiagnostics(GetSensorDiagnosticsRequest request)`

**Responsibility**: Return sensor health and performance metrics.

**Input Validation**:
- SensorId: Must not be empty

**Deterministic Generation**:
- Seed: Hash of `sensorId`
- Metrics generated with realistic ranges:
  - DetectionEfficiency: 85-100%
  - GpsVisibility: 90-100%
  - TrackedSatellites: 12-20
  - NoiseLevel: 0.5-2.0
  - Uptime: 95-100%
  - Snr (Signal-to-Noise Ratio): 15-35
  - Status: "healthy" (90% chance) or "degraded" (10% chance)

**Output**:
```csharp
{
  DetectionEfficiency: double,    // 85-100
  GpsVisibility: double,          // 90-100
  TrackedSatellites: int,         // 12-20
  NoiseLevel: double,             // 0.5-2.0
  Uptime: double,                 // 95-100
  LastCalibration: string,        // YYYY-MM-DD (0-30 days ago)
  Snr: double,                    // 15-35
  Status: string                  // "healthy" or "degraded"
}
```

---

### Informer Status Module

**Interface**: `IInformerStatusModule`

**Method**: `GetInformerStatus(GetInformerStatusRequest request)`

**Responsibility**: Return warning device status.

**Input Validation**:
- At least one of InformerId or Zone must be provided

**Deterministic Generation**:
- Seed: Hash of `informerId ?? zone ?? "unknown"`
- Status: "active" (95% chance) or "inactive" (5% chance)
- PowerStatus: "normal" (90% chance) or "low" (10% chance)
- DeviceType: Randomly selected from [Strobe, Horn, Siren, Hybrid]
- Zone: Derived from seed if not provided (Zone-0 to Zone-9)

**Output**:
```csharp
{
  Status: string,                // "active" or "inactive"
  LastActivation: string,        // ISO 8601 timestamp (0-1440 minutes ago)
  PowerStatus: string,           // "normal" or "low"
  Zone: string,                  // Zone identifier
  DeviceType: string             // "Strobe", "Horn", "Siren", or "Hybrid"
}
```

---

## Layer 4: Shared Services

### Dependency Injection (Program.cs)

**Registered Services**:
```csharp
services.AddScoped<IStrikeDetectionModule, StrikeDetectionModule>();
services.AddScoped<IWeatherForecastModule, WeatherForecastModule>();
services.AddScoped<ISensorDiagnosticsModule, SensorDiagnosticsModule>();
services.AddScoped<IInformerStatusModule, InformerStatusModule>();
services.AddScoped<IToolRegistry, ToolRegistry>();
services.AddControllers();
services.AddLogging();
```

**Lifetimes**:
- `Scoped`: One instance per HTTP request (modules, registry)
- `Transient`: New instance each time (loggers)
- `Singleton`: One instance for application lifetime (IConfiguration, IHostApplicationLifetime)

### Logging (Program.cs + appsettings.json)

**Configuration**:
- Console provider with JSON output
- UTC timestamps in ISO 8601 format
- Scopes enabled (request context)
- Log levels: INFO (default), DEBUG (development)

**Usage**:
```csharp
_logger.LogInformation("Strike detection requested: location=({Latitude},{Longitude}), ...", ...);
```

**Output Example**:
```json
{
  "timestamp": "2026-10-04T15:30:00Z",
  "level": "Information",
  "logger": "LightningMcpServer.DomainModules.StrikeDetectionModule",
  "message": "Strike detection result: count=8, nearest=3.45km"
}
```

### Configuration (appsettings.json + Environment Variables)

**Environment Variables**:
- `LISTEN_PORT` — HTTP server port (default: 8000)
- `LOG_LEVEL` — Logging level (default: INFO)
- `ASPNETCORE_URLS` — Kestrel binding URL (default: http://0.0.0.0:8000)

**appsettings.json**:
- Logging levels per namespace
- AllowedHosts configuration

---

## Data Flow Example: Strike Detection Request

```
1. HTTP POST /mcp/messages
   {
     "method": "tools/call",
     "params": {
       "name": "get_lightning_strikes_near_location",
       "arguments": { "latitude": 30.27, "longitude": -97.74, "radius": 50 }
     }
   }

2. McpController.HandleMcpMessage() receives request
   → Routes to HandleToolCall()

3. McpController.HandleToolCall() parses request
   → Calls ToolRegistry.CallTool("get_lightning_strikes_near_location", arguments)

4. ToolRegistry.HandleStrikeDetection() parses arguments
   → Creates GetLightningStrikesNearLocationRequest
   → Calls StrikeDetectionModule.GetLightningStrikesNearLocation()

5. StrikeDetectionModule validates input
   → Generates seed from coordinates
   → Creates Random(seed)
   → Generates 2-12 strikes deterministically
   → Calculates nearest strike distance
   → Logs result
   → Returns GetLightningStrikesNearLocationResponse

6. Response serialized to JSON
   → HTTP 200 OK with JSON response body
```

---

## Testing Strategy

### Unit Tests (Domain Modules)

Each module has 3-5 unit tests covering:
- Valid input → Returns expected response
- Deterministic output (same input → same output)
- Invalid input → Throws ArgumentException

Example (StrikeDetectionModuleTests):
- ValidLocation_ReturnsStrikes
- SameLocation_ReturnsDeterministicResults
- InvalidRadius_ThrowsArgumentException
- InvalidRadiusUnit_ThrowsArgumentException
- DifferentLocations_ReturnsDifferentResults

### Integration Tests (MCP Server)

7 integration tests covering:
- HealthCheck_ReturnsHealthy
- ListTools_ReturnsAllTools
- CallStrikeTool_WithValidInput_ReturnsStrikes
- CallWeatherTool_WithValidInput_ReturnsForecast
- CallSensorTool_WithValidInput_ReturnsDiagnostics
- CallInformerTool_WithValidInput_ReturnsStatus
- UnknownTool_ReturnsErrorResponse

---

## Phase 2 Migration Path

This architecture supports Phase 2 enhancements:

1. **Shared Library Extension** — Add `LightningCommon` interfaces for real data sources
2. **Module Replacement** — Swap stub implementations with real providers
3. **Session Management** — Add session store to Shared Services
4. **Intent Classification** — Add Claude SDK integration to Coordinator Agent

All changes localized to domain modules; controllers and MCP layer remain unchanged.

---

**Last Updated**: 2026-10-04
