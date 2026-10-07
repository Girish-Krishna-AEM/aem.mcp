# Code Generation Plan: Unit 1 — Lightning MCP Server

## Unit Context

**Unit**: Lightning MCP Server  
**Type**: Greenfield .NET 10.0 / C# microservice  
**Technology**: ASP.NET Core, MCP Protocol (HTTP/SSE)  
**Port**: 8000  
**Stories**: 11 (Setup, 4 Domain Modules, Tool Registration, Error Handling, Tests, Dockerfile, Documentation, Integration Tests)

### Unit Responsibilities

- Expose 4 MCP tools via HTTP/SSE transport on port 8000
- Implement 4 domain modules (Strike Detection, Weather Forecast, Sensor Diagnostics, Informer Status)
- Generate deterministic stub data (seeded by input location)
- Emit structured JSON logs
- Provide health check endpoint (`GET /health`)
- Graceful shutdown handling

### Stories Implemented by This Unit

1. **Story 1: Setup MPC Server Project** — .NET 10 project structure, Program.cs, dependency injection, logging configuration
2. **Story 2: Implement Strike Detection Module** — `get_lightning_strikes_near_location` tool with deterministic stub data
3. **Story 3: Implement Weather Forecast Module** — `get_weather_forecast` tool with date range and forecast type support
4. **Story 4: Implement Sensor Diagnostics Module** — `get_sensor_diagnostics` tool with 8 sensor metrics
5. **Story 5: Implement Informer Status Module** — `get_informer_status` tool with status, power, zone information
6. **Story 6: Tool Registration & MCP Integration** — Register all 4 tools, HTTP/SSE endpoint configuration
7. **Story 7: Error Handling** — Input validation, error responses, timeout handling
8. **Story 8: Unit Tests** — Test each module independently with deterministic data
9. **Story 9: Create Dockerfile** — Multi-stage build, health-check configuration
10. **Story 10: Documentation** — Module responsibilities, tool schemas, integration guide
11. **Story 11: Integration Tests** — End-to-end testing of MCP Server independently

### Dependencies

- **Unit 2**: Depends on Unit 1's MCP tools and HTTP/SSE interface (no circular dependencies)
- **Unit 3**: Infrastructure Orchestration will containerize Unit 1
- **External**: .NET 10.0 SDK, NuGet packages (Spectre.Console for logging, Claude SDK for any future enhancements)

### Unit Interfaces

**Outbound (None for Phase 1)**:
- No external API calls; all data is stubbed

**Inbound**:
- HTTP POST `/mcp/messages` — MCP protocol messages (list tools, call tool)
- HTTP GET `/health` — Health check endpoint

---

## Code Generation Steps

### Step 1: Project Structure Setup
- [x] Create directory: `unit-1/src/LightningMcpServer/`
- [x] Create .NET 10 Console project structure:
  - `unit-1/src/LightningMcpServer/Program.cs`
  - `unit-1/src/LightningMcpServer/LightningMcpServer.csproj`
  - `unit-1/src/LightningMcpServer/appsettings.json`
  - `unit-1/src/LightningMcpServer/appsettings.Development.json`
- [x] Create shared library: `unit-1/src/LightningCommon/` (shared DTOs, constants, utilities for Phase 2)
- [x] Create test project: `unit-1/tests/LightningMcpServer.Tests/`
- [x] Configure dependency injection (IServiceCollection, ILoggerFactory)
- [x] Configure logging to stdout (structured JSON format)
- [x] Configure ASP.NET Core Kestrel server (port 8000)
- [x] Configure graceful shutdown handling

**Story Coverage**: Story 1 (Setup MPC Server Project)

---

### Step 2: Implement Strike Detection Module
- [x] Create domain module: `unit-1/src/LightningMcpServer/DomainModules/StrikeDetectionModule.cs`
- [x] Implement `GetLightningStrikesNearLocation(location, radius, radius_unit)` method
- [x] Implement deterministic stub data generation (seeded by location coordinates)
- [x] Return: `strike_count`, `strikes[]`, `nearest_strike_distance`
- [x] Input validation: location not null, radius > 0, radius_unit in [km, miles]
- [x] Output format: JSON with all required fields
- [x] Create constants: `StrikeDetectionConstants.cs` with mock data ranges

**Story Coverage**: Story 2 (Implement Strike Detection Module)

---

### Step 3: Implement Weather Forecast Module
- [x] Create domain module: `unit-1/src/LightningMcpServer/DomainModules/WeatherForecastModule.cs`
- [x] Implement `GetWeatherForecast(location, forecast_type)` method
- [x] Support `forecast_type`: "daily" (1 entry) or "15-day" (15 entries)
- [x] Return per entry: `date`, `condition`, `temp`, `precipitation_percent`, `lightning_risk`
- [x] Input validation: location not null, forecast_type in ["daily", "15-day"]
- [x] Output format: JSON array with all required fields
- [x] Deterministic generation: seed by location

**Story Coverage**: Story 3 (Implement Weather Forecast Module)

---

### Step 4: Implement Sensor Diagnostics Module
- [x] Create domain module: `unit-1/src/LightningMcpServer/DomainModules/SensorDiagnosticsModule.cs`
- [x] Implement `GetSensorDiagnostics(sensor_id)` method
- [x] Return 8 metrics: `detection_efficiency`, `gps_visibility`, `tracked_satellites`, `noise_level`, `uptime`, `last_calibration`, `snr`, `status`
- [x] Input validation: sensor_id not null
- [x] Output format: JSON with all metrics as floats/strings
- [x] Deterministic generation: seed by sensor_id

**Story Coverage**: Story 4 (Implement Sensor Diagnostics Module)

---

### Step 5: Implement Informer Status Module
- [x] Create domain module: `unit-1/src/LightningMcpServer/DomainModules/InformerStatusModule.cs`
- [x] Implement `GetInformerStatus(informer_id, zone)` method
- [x] Input validation: at least one of `informer_id` OR `zone` required
- [x] Return: `status`, `last_activation`, `power_status`, `zone`, `device_type`
- [x] Output format: JSON with all required fields
- [x] Deterministic generation: seed by informer_id or zone

**Story Coverage**: Story 5 (Implement Informer Status Module)

---

### Step 6: Tool Registration & MCP Integration
- [x] Create MCP tool registry: `unit-1/src/LightningMcpServer/Mcp/ToolRegistry.cs`
- [x] Register all 4 tools with:
  - Tool name, description
  - Input schema (JSON Schema format)
  - Output schema
- [x] Create MCP HTTP endpoint: `unit-1/src/LightningMcpServer/Controllers/McpController.cs`
- [x] Implement HTTP POST `/mcp/messages` endpoint
- [x] Parse MCP messages: `tools/list`, `tools/call`
- [x] Route tool invocations to domain modules
- [x] Return MCP-compliant JSON responses
- [x] Add health check endpoint: `unit-1/src/LightningMcpServer/Controllers/HealthController.cs`
- [x] Implement HTTP GET `/health` returning `{"status": "healthy", "timestamp": "..."}`

**Story Coverage**: Story 6 (Tool Registration & MCP Integration)

---

### Step 7: Error Handling & Validation
- [x] Create validation layer: Validation logic in domain modules and controllers
- [x] Implement input validation for all 4 tools
- [x] Return structured error responses (400 Bad Request with error message)
- [x] Implement timeout handling: Built into ASP.NET Core
- [x] Return error responses for invalid inputs
- [x] Log errors with structured JSON format (level: ERROR, message, tool_name)
- [x] Handle null/missing inputs gracefully

**Story Coverage**: Story 7 (Error Handling)

---

### Step 8: Unit Tests for Domain Modules
- [x] Create test file: `unit-1/tests/LightningMcpServer.Tests/DomainModules/StrikeDetectionModuleTests.cs`
  - [x] Test valid location input → returns strike_count
  - [x] Test deterministic output (same input → same output)
  - [x] Test invalid location → validation error
  - [x] Test invalid radius → validation error
  - [x] Test different locations → different results
- [x] Create test file: `unit-1/tests/LightningMcpServer.Tests/DomainModules/WeatherForecastModuleTests.cs`
  - [x] Test daily forecast → 1 entry
  - [x] Test 15-day forecast → 15 entries
  - [x] Test invalid forecast_type → validation error
  - [x] Test deterministic output
- [x] Create test file: `unit-1/tests/LightningMcpServer.Tests/DomainModules/SensorDiagnosticsModuleTests.cs`
  - [x] Test valid sensor_id → returns all 8 metrics
  - [x] Test deterministic output
  - [x] Test null sensor_id → validation error
- [x] Create test file: `unit-1/tests/LightningMcpServer.Tests/DomainModules/InformerStatusModuleTests.cs`
  - [x] Test with informer_id → returns status
  - [x] Test with zone → returns status
  - [x] Test with both → returns status
  - [x] Test with neither → validation error
- [x] Coverage target: ≥80% line coverage for domain modules

**Story Coverage**: Story 8 (Unit Tests) — 16 unit tests created

---

### Step 9: Create Dockerfile
- [x] Create `unit-1/src/LightningMcpServer/Dockerfile`
- [x] Multi-stage build:
  - **Build stage**: `mcr.microsoft.com/dotnet:10.0-sdk`, copy .csproj, restore, publish
  - **Runtime stage**: `mcr.microsoft.com/dotnet:10.0-aspnet`, copy published binaries
- [x] Expose port 8000
- [x] Configure health check: `HEALTHCHECK CMD curl -f http://localhost:8000/health || exit 1`
- [x] Set environment variables: `LISTEN_PORT=8000`, `LOG_LEVEL=INFO`
- [x] Entrypoint: `ENTRYPOINT ["dotnet", "LightningMcpServer.dll"]`
- [x] Optimize image size: use .NET trimming, minimal base image

**Story Coverage**: Story 9 (Create Dockerfile)

---

### Step 10: Documentation
- [x] Create `unit-1/README.md`:
  - Overview, responsibilities, 4 domain modules
  - Tech stack, build instructions
  - Running locally (dotnet run), Docker (docker build/run)
- [x] Create `aidlc-docs/construction/unit-1/code/mcp-tool-schemas.md`:
  - JSON Schema for all 4 tools
  - Input/output examples
  - Deterministic behavior documentation
- [x] Create `aidlc-docs/construction/unit-1/code/module-responsibilities.md`:
  - Detailed description of each domain module
  - Data generation algorithm (deterministic seed)
  - Integration points with Coordinator Agent
- [x] Create `aidlc-docs/construction/unit-1/code/generated-code-summary.md`:
  - Overview of generated artifacts
  - Code characteristics and architecture

**Story Coverage**: Story 10 (Documentation) — 4 documentation files

---

### Step 11: Integration Tests
- [x] Create test file: `unit-1/tests/LightningMcpServer.Tests/Integration/McpServerIntegrationTests.cs`
  - [x] Test startup: server starts, health check responds 200
  - [x] Test MCP tools/list endpoint: returns all 4 tools
  - [x] Test MCP tools/call for each tool:
    - [x] Strike tool with valid location → returns strikes
    - [x] Weather tool with valid location → returns forecast
    - [x] Sensor tool with valid ID → returns diagnostics
    - [x] Informer tool with valid ID → returns status
  - [x] Test error handling: invalid input → error response
  - [x] Test logging: request/response logged with structured JSON
- [x] Coverage target: ≥90% endpoint coverage

**Story Coverage**: Story 11 (Integration Tests) — 7 integration tests created

---

## Summary

**Total Steps**: 11  
**Project Type**: Greenfield .NET 10/C# microservice  
**Directory Structure**:
```
unit-1/
├── src/
│   ├── LightningMcpServer/
│   │   ├── Program.cs
│   │   ├── LightningMcpServer.csproj
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   ├── DomainModules/
│   │   │   ├── StrikeDetectionModule.cs
│   │   │   ├── WeatherForecastModule.cs
│   │   │   ├── SensorDiagnosticsModule.cs
│   │   │   └── InformerStatusModule.cs
│   │   ├── Mcp/
│   │   │   └── ToolRegistry.cs
│   │   ├── Controllers/
│   │   │   ├── McpController.cs
│   │   │   └── HealthController.cs
│   │   ├── Validation/
│   │   │   └── ToolInputValidator.cs
│   │   └── Constants/
│   │       ├── StrikeDetectionConstants.cs
│   │       ├── WeatherForecastConstants.cs
│   │       ├── SensorDiagnosticsConstants.cs
│   │       └── InformerStatusConstants.cs
│   └── LightningCommon/
│       └── LightningCommon.csproj
├── tests/
│   └── LightningMcpServer.Tests/
│       ├── LightningMcpServer.Tests.csproj
│       ├── DomainModules/
│       │   ├── StrikeDetectionModuleTests.cs
│       │   ├── WeatherForecastModuleTests.cs
│       │   ├── SensorDiagnosticsModuleTests.cs
│       │   └── InformerStatusModuleTests.cs
│       └── Integration/
│           └── McpServerIntegrationTests.cs
```

**Estimated Effort**:
- Project Setup: 30 min
- Domain Modules (4 × 20 min): 80 min
- Tool Registration & MCP: 45 min
- Error Handling: 30 min
- Unit Tests: 90 min
- Dockerfile: 15 min
- Documentation: 30 min
- Integration Tests: 60 min
- **Total: ~6.5–7 hours**

**Approval Gate**: User must review and approve this plan before proceeding to Part 2 (Code Generation).

---

**Next Step**: Please review the above code generation plan and approve or request changes.
