# Unit of Work Definitions: Lightning Detection MCP POC (Phase 1)

## Overview

This document defines the three logical units of work for the Lightning Detection MCP POC (Phase 1). Each unit represents a manageable development scope with clear responsibilities, boundaries, and code organization patterns.

The three units decompose the system into:
1. **Lightning MCP Server Unit** — Standalone .NET 10 service exposing 4 MCP tools via HTTP/SSE transport
2. **Coordinator Agent Unit** — Lightweight .NET 10 agent service implementing intent routing and tool invocation
3. **Infrastructure Orchestration Unit** — Docker Compose orchestration, containerization, and EC2 deployment patterns

Units 1 & 2 communicate via MCP protocol (HTTP/SSE), providing loose coupling for independent development and testing. Unit 3 orchestrates both services and manages deployment artifacts. Shared utilities (logging, health checks) are provided by the LightningCommon project dependency.

---

## Unit 1: Lightning MCP Server

### Scope

**Functional Requirements Owned**:
- FR-1.1: `get_lightning_strikes_near_location` — Returns stubbed lightning strike/proximity data
- FR-1.2: `get_weather_forecast` — Returns stubbed weather forecast for a location
- FR-1.3: `get_sensor_diagnostics` — Returns stubbed health diagnostics for a lightning sensor
- FR-1.4: `get_informer_status` — Returns stubbed status for warning devices (strobes/horns)

**Part of FR-3 Owned**:
- MCP protocol server-side implementation (HTTP/SSE transport)
- Tool schema compliance and well-formed JSON responses
- Input validation at the MCP tool level

**Non-Functional Requirements Owned**:
- Structured console logging (request/response, errors)
- Health-check endpoint (`GET /health` → 200 with `{"status": "healthy"}`)

### Responsibilities

Unit 1 is responsible for:
- Exposing 4 MCP tools over HTTP/SSE protocol (port 8000)
- Implementing stub/synthetic data generation for all tools (deterministic, representative data)
- Validating tool inputs and returning clear error messages for invalid requests
- Emitting structured JSON logs for debugging and monitoring
- Providing `/health` endpoint for Docker Compose and liveness checks
- Documenting tool responsibilities and how to integrate real data sources in Phase 2

Unit 1 is **NOT** responsible for:
- Coordinator Agent logic (intent routing, parameter extraction)
- Docker Compose orchestration (owned by Unit 3)
- Real data source integrations (Phase 2)
- Inter-service coordination or session/conversation state
- Authentication, authorization, or PII redaction (Phase 2+)

### Internal Domain Modules (Pre-Structure for Phase 2/3 Refactoring)

The 4 MCP tools are logically grouped as internal domain modules within a single .csproj. This structure enables future refactoring into independent microservices without rewriting tool logic.

**Strike Detection Module (FR-1.1)**
- Implements `get_lightning_strikes_near_location` tool
- Input: location (City+State or ZIP), radius (numeric), radius_unit (miles|km)
- Output: JSON with strike_count, strikes[], nearest_strike_distance
- Stub logic: Generate N synthetic strikes with random offsets from queried location, distances, timestamps, intensities

**Weather Forecast Module (FR-1.2)**
- Implements `get_weather_forecast` tool
- Input: location (City+State), forecast_type (daily|15-day)
- Output: JSON with forecast array (1 entry for daily, 15 for 15-day)
- Stub logic: Generate deterministic forecast entries with date, condition, temp high/low, precipitation %, lightning-risk indicator

**Sensor Diagnostics Module (FR-1.3)**
- Implements `get_sensor_diagnostics` tool
- Input: sensor_id (string)
- Output: JSON with Detection Efficiency (%), GPS Visibility, Tracked Satellites, Noise Level, Uptime, Last Calibration, SNR, Status
- Stub logic: Generate realistic diagnostic data based on sensor_id, with consistent patterns

**Informer Status Module (FR-1.4)**
- Implements `get_informer_status` tool
- Input: informer_id OR zone (one required)
- Output: JSON with Status (Active|Idle|Fault), Last Activation, Power Status, Zone, Device Type
- Stub logic: Generate device status based on informer_id/zone, with realistic state transitions

### Internal Architecture

**Component Interactions**:
- Program.cs: Starts HTTP server (port 8000), registers MCP SDK, configures dependency injection, sets up structured logging
- MCP Tool Registry: Discovers and registers all 4 tools at startup; routes incoming MCP calls to appropriate module
- Domain Modules: Each module is a .cs class with a method implementing the tool logic (input validation, stub generation, output formatting)
- Shared Logging: Uses ILogger interface injected from LightningCommon; emits structured JSON logs
- Health Check: GET /health endpoint uses IHealthCheckProvider from LightningCommon

**Design Principles**:
- Stateless: Each tool invocation is independent; no inter-request state
- Dependency Injection: All services (logging, config) injected at startup; testable in isolation
- Module Independence: Each domain module can be extracted to a separate .csproj in Phase 2 with minimal changes
- Stub Determinism: Stub data generation is deterministic (seeded by inputs) for reproducible testing

### Technology Stack

- **.NET 10.0** — Latest LTS runtime
- **C#** — Language
- **MCP SDK** — MCP protocol implementation (HTTP/SSE transport)
- **ASP.NET Core** — HTTP server framework (for HTTP/SSE transport)
- **Dependency Injection** — Built-in .NET ServiceCollection for DI
- **Structured Logging** — Serilog (recommended) or built-in ILogger with JSON formatters
- **LightningCommon** — Shared library for logging setup, health-check patterns

### Code Organization

```
src/LightningMcpServer/
├── LightningMcpServer.csproj
├── Program.cs
├── Modules/
│   ├── StrikeDetection/
│   │   ├── StrikeDetectionModule.cs
│   │   ├── StrikeDetectionService.cs
│   │   ├── Models/
│   │   │   ├── StrikeQuery.cs
│   │   │   ├── StrikeResult.cs
│   │   │   └── LightningStrike.cs
│   │   └── Constants.cs
│   ├── WeatherForecast/
│   │   ├── WeatherForecastModule.cs
│   │   ├── WeatherForecastService.cs
│   │   ├── Models/
│   │   │   ├── ForecastQuery.cs
│   │   │   ├── ForecastResult.cs
│   │   │   └── ForecastEntry.cs
│   │   └── Constants.cs
│   ├── SensorDiagnostics/
│   │   ├── SensorDiagnosticsModule.cs
│   │   ├── SensorDiagnosticsService.cs
│   │   ├── Models/
│   │   │   ├── DiagnosticsQuery.cs
│   │   │   └── DiagnosticsResult.cs
│   │   └── Constants.cs
│   └── InformerStatus/
│       ├── InformerStatusModule.cs
│       ├── InformerStatusService.cs
│       ├── Models/
│       │   ├── InformerQuery.cs
│       │   └── InformerStatusResult.cs
│       └── Constants.cs
├── Common/
│   ├── McpToolRegistry.cs
│   ├── Models/
│   │   ├── ErrorResponse.cs
│   │   └── ToolInputSchema.cs
│   └── Constants/
│       ├── ToolNames.cs
│       └── ValidationRules.cs
├── Logging/
├── HealthCheck/
├── Dockerfile
├── README.md
└── appsettings.json
```

### Dependencies

**Internal**:
- `LightningCommon` — For shared logging, health-check patterns, common error models

**External**:
- MCP SDK (.NET package)
- ASP.NET Core (HTTP framework)
- Serilog or ILogger (logging)
- .NET 10.0 runtime

### Not Owned by This Unit

- Coordinator Agent code (Unit 2)
- Docker Compose orchestration (Unit 3)
- Real data source integrations (Phase 2)
- Multi-turn session memory (Phase 2+)
- Authentication, authorization, PII redaction (Phase 2+)
- Observability beyond basic logging (Phase 2+)

---

## Unit 2: Coordinator Agent

### Scope

**Functional Requirements Owned**:
- FR-2: Intent classification, parameter extraction, tool invocation, result formatting, missing-parameter handling

**Part of FR-3 Owned**:
- HTTP endpoint for user queries (POST /query)
- MCP client-side implementation (HTTP/SSE transport)

**Non-Functional Requirements Owned**:
- Structured console logging
- Health-check endpoint (`GET /health`)

### Responsibilities

Unit 2 is responsible for:
- Receiving natural-language user queries via HTTP endpoint (POST /query)
- Classifying query intent into one of 4 MCP tools using LLM (Claude SDK)
- Extracting required parameters from the query text
- Invoking the correct tool on the Lightning MPC Server via MCP HTTP/SSE protocol
- Formatting tool responses for the user
- Asking for missing required parameters (simple one-turn clarification)
- Emitting structured JSON logs for debugging
- Providing `/health` endpoint for liveness checks

Unit 2 is **NOT** responsible for:
- Lightning MCP Server implementation (Unit 1)
- Docker Compose orchestration (Unit 3)
- LLM model fine-tuning (Phase 2+)
- Multi-turn conversation memory (Phase 2+)
- Routing across multiple domain agents (Phase 2+)
- Authentication, authorization (Phase 2+)

### Internal Architecture

**Component Interactions**:
- IntentClassifier — Maps natural-language query to one of 4 intents using LLM
- ParameterExtractor — Extracts tool parameters from query
- MPC ClientWrapper — HTTP/SSE client for invoking Lightning MPC Server
- ResultFormatter — Formats tool output for user
- MissingParameterHandler — Asks for missing required parameters
- HTTP Handler (ASP.NET Core endpoints for query submission)

### Technology Stack

- **.NET 10.0** — Latest LTS runtime
- **C#** — Language
- **Claude SDK** (or similar LLM) — For intent classification NLP
- **MCP SDK** — MCP client implementation (HTTP/SSE)
- **ASP.NET Core** — HTTP server framework
- **Structured Logging** — Serilog or built-in ILogger
- **LightningCommon** — Shared library for logging, health checks

### Code Organization

```
src/CoordinatorAgent/
├── CoordinatorAgent.csproj
├── Program.cs
├── Components/
│   ├── IntentClassifier.cs
│   ├── ParameterExtractor.cs
│   ├── MpcClientWrapper.cs
│   ├── ResultFormatter.cs
│   └── MissingParameterHandler.cs
├── Http/
│   ├── QueryEndpoint.cs
│   ├── HealthCheckEndpoint.cs
│   └── Models/
│       ├── QueryRequest.cs
│       ├── QueryResponse.cs
│       └── ErrorResponse.cs
├── Config/
│   ├── MpcServerConfig.cs
│   └── LlmConfig.cs
├── Logging/
├── HealthCheck/
├── Dockerfile
├── README.md
└── appsettings.json
```

### Integration Contract with Unit 1

**Service Discovery**:
- Coordinator reaches Lightning MPC Server via DNS name: `lightning-mpc-server` (port 8000)
- Environment variable: `MPC_SERVER_URL=http://lightning-mpc-server:8000`

**MCP Tool Schemas**:
Coordinator must know the 4 tool names and their input/output contracts (documented in OpenAPI spec).

**Error Handling**:
- MPC Server returns well-formed error responses for validation failures
- Coordinator handles errors gracefully and returns user-friendly messages

**Backward Compatibility**:
- New tools can be added without breaking Coordinator (loose coupling via tool registry)
- Tool schema changes must remain backward-compatible

### Dependencies

**Internal**:
- `LightningCommon` — Shared logging, health-check patterns

**External**:
- Claude SDK (or compatible LLM library)
- MCP SDK (.NET package)
- ASP.NET Core
- .NET 10.0 runtime

### Not Owned by This Unit

- Lightning MCP Server implementation (Unit 1)
- Docker Compose orchestration (Unit 3)
- LLM model optimization (Phase 2+)
- Multi-turn conversation memory (Phase 2+)
- Routing to multiple domain agents (Phase 2+)
- Authentication, authorization (Phase 2+)

---

## Unit 3: Infrastructure Orchestration

### Scope

**Non-Functional Requirements Owned**:
- NFR-2: Containerization & Deployment (Docker images, Docker Compose, EC2)
- NFR-3 (orchestration part): Health-check configuration

### Responsibilities

Unit 3 is responsible for:
- Defining Dockerfile for Unit 1 and Unit 2
- Creating docker-compose.yml that orchestrates both services
- Configuring service networking (docker network, DNS names)
- Setting up health-check directives for both services
- Managing environment variables
- Providing EC2 deployment recommendations
- Documenting deployment procedures

Unit 3 is **NOT** responsible for:
- Individual service code (Unit 1 & 2)
- Dockerfile content details (delegated to Unit 1 & 2)
- LLM configuration (Unit 2 responsibility)
- MCP protocol implementation (Unit 1 & 2 responsibility)

### Internal Structure

**Artifacts**:
1. **docker-compose.yml** — Main orchestration file
2. **Dockerfiles** — For Unit 1 and Unit 2
3. **.env.example** — Template for environment variables
4. **EC2 Deployment Guide** — README or CloudFormation template
5. **DEPLOYMENT.md** — Comprehensive deployment documentation

### Technology Stack

- **Docker** — Container runtime
- **Docker Compose** — Service orchestration
- **AWS EC2** — Target deployment environment
- **.NET 10.0 runtime images** — Base images for Dockerfiles

### Code Organization

```
C:\aem\ai-agentic\mcp\
├── docker-compose.yml
├── docker/
│   ├── LightningMcpServer.Dockerfile
│   ├── CoordinatorAgent.Dockerfile
│   └── .dockerignore
├── .env.example
├── ec2-deployment/
│   ├── README.md
│   ├── ec2-launch.sh
│   └── cloudformation-template.yaml
├── docs/
│   ├── DEPLOYMENT.md
│   ├── TROUBLESHOOTING.md
│   └── ARCHITECTURE.md
└── scripts/
    ├── build-images.sh
    ├── start-local.sh
    ├── health-check.sh
    └── logs.sh
```

### Docker Compose Structure

**Services**:
- `lightning-mpc-server` — Port 8000, depends on none, health-check /health
- `coordinator-agent` — Port 5000, depends_on lightning-mpc-server (healthy), health-check /health

**Networking**: docker network `lightning-network` (bridge)

**Environment Variables**: LOG_LEVEL, ASPNETCORE_URLS, MPC_SERVER_URL, CLAUDE_API_KEY

**Health Checks**: interval 10s, timeout 5s, retries 3, start_period 5s

### EC2 Deployment Pattern

**Instance Type**: t3.small or t3.medium
**AMI**: Amazon Linux 2023 or Ubuntu 22.04 LTS
**Security Groups**: SSH (22), HTTP (5000), MCP (8000)
**Startup**: docker-compose up via systemd or user data script

### Dependencies

**Build-Time**:
- Unit 1 & 2 source code and Dockerfiles
- .NET 10.0 SDK and runtime

**Runtime**:
- Docker daemon and Docker Compose CLI
- Network connectivity between containers

### Not Owned by This Unit

- Individual service implementation (Unit 1 & 2)
- LLM configuration (Unit 2)
- MCP protocol details (Unit 1)
- Real data integrations (Phase 2)

---

## Cross-Unit Integration

### Service-to-Service Communication

**Coordinator Agent ↔ Lightning MPC Server**:
- Protocol: MCP (HTTP/SSE)
- Service Discovery: Docker Compose DNS, port 8000
- Environment Variable: `MPC_SERVER_URL`
- Network: docker network `lightning-network`

### Deployment Model

**Phase 1**:
- Monorepo: All code under `src/`
- Single docker-compose.yml orchestrates both services
- Locked versions: Both deployed together
- Single EC2 instance

**Phase 2+**:
- Independent versioning and deployment (if backward-compatible MCP schemas)
- Multiple MCP Servers (4 domain-specific services)
- Load balancing, multi-AZ deployment

### Testing Strategy

- **Unit Tests**: Per service, independent unit testing
- **Integration Tests**: Cross-service (Coordinator → MPC Server)
- **End-to-End Tests**: Full docker-compose stack

### Shared Library (LightningCommon)

**Location**: `src/LightningCommon/`

**Responsibilities**:
- Structured logging setup
- Health-check provider
- Common error response models
- Dependency injection extensions

---

## Phase 1 → Phase 2 Refactoring Path

### Minimal Refactoring for Domain Separation

Each internal domain module can be extracted to an independent MPC Server with minimal effort:

1. Move StrikeDetection/ → LightningStrikeServer.csproj
2. Move WeatherForecast/ → LightningWeatherServer.csproj
3. Move SensorDiagnostics/ → LightningDeviceServer.csproj
4. Move InformerStatus/ → LightningWarningServer.csproj
5. Update Coordinator to route to multiple MPC Servers
6. Extend docker-compose.yml to include all services

**Effort Estimate**: 1-2 developer-days per service extraction (move code, update DI, test).

---

**Unit definitions complete. Ready for Code Generation phase.**
