# Lightning MCP Server

A .NET 10.0 / C# microservice implementing the Model Context Protocol (MCP) for lightning weather detection and warning systems.

## Overview

The Lightning MCP Server exposes 4 MCP tools via HTTP/SSE transport:

1. **get_lightning_strikes_near_location** — Query lightning strike data for a location
2. **get_weather_forecast** — Get weather forecast with lightning risk
3. **get_sensor_diagnostics** — Query lightning detection sensor health metrics
4. **get_informer_status** — Query warning device status (strobe, horn, siren, hybrid)

All responses use deterministic stub data, ensuring reproducible testing and Phase 2 integration compatibility.

## Technology Stack

- **.NET 10.0** — Cross-platform runtime
- **C#** — Primary language
- **ASP.NET Core** — Web framework for Kestrel HTTP server
- **MCP (Model Context Protocol)** — HTTP/SSE tool invocation protocol
- **Structured JSON Logging** — stdout-based observability

## Project Structure

```
unit-1/
├── src/
│   ├── LightningMcpServer/              # Main MCP Server service
│   │   ├── Program.cs                   # Startup configuration, DI setup
│   │   ├── LightningMcpServer.csproj    # Project file
│   │   ├── appsettings.json             # Configuration
│   │   ├── Dockerfile                   # Multi-stage container build
│   │   ├── Controllers/                 # HTTP endpoints
│   │   │   ├── McpController.cs         # /mcp/messages endpoint
│   │   │   └── HealthController.cs      # /health endpoint
│   │   ├── DomainModules/               # Business logic
│   │   │   ├── StrikeDetectionModule.cs
│   │   │   ├── WeatherForecastModule.cs
│   │   │   ├── SensorDiagnosticsModule.cs
│   │   │   └── InformerStatusModule.cs
│   │   └── Mcp/                         # MCP protocol handling
│   │       ├── IToolRegistry.cs
│   │       └── ToolRegistry.cs
│   └── LightningCommon/                 # Shared library (Phase 2 extensibility)
│       ├── LightningCommon.csproj
│       └── DomainModels.cs              # Request/Response DTOs
└── tests/
    └── LightningMcpServer.Tests/        # Unit and integration tests
        ├── DomainModules/               # Module unit tests
        └── Integration/                 # End-to-end tests
```

## Running Locally

### Prerequisites

- .NET 10.0 SDK

### Build

```bash
cd unit-1
dotnet build
```

### Run

```bash
cd unit-1/src/LightningMcpServer
dotnet run --environment Development
```

Server listens on `http://localhost:8000`

### Test

```bash
cd unit-1
dotnet test
```

## Docker

### Build Image

```bash
docker build -f unit-1/src/LightningMcpServer/Dockerfile -t lightning-mcp-server:latest .
```

### Run Container

```bash
docker run -p 8000:8000 \
  -e LISTEN_PORT=8000 \
  -e LOG_LEVEL=INFO \
  lightning-mcp-server:latest
```

## API Endpoints

### Health Check

```bash
GET http://localhost:8000/health

# Response
{"status": "healthy", "timestamp": "2026-10-04T15:30:00Z"}
```

### MCP Protocol

```bash
POST http://localhost:8000/mcp/messages
Content-Type: application/json

# List Tools
{
  "method": "tools/list"
}

# Call Tool
{
  "method": "tools/call",
  "params": {
    "name": "get_lightning_strikes_near_location",
    "arguments": {
      "latitude": 30.2672,
      "longitude": -97.7431,
      "radius": 50,
      "radiusUnit": "km"
    }
  }
}
```

## Deterministic Stub Data

All tools generate deterministic responses based on input:

- **Strike Detection**: Seeded by location coordinates → same location always returns same strikes
- **Weather Forecast**: Seeded by location → reproducible forecast data
- **Sensor Diagnostics**: Seeded by sensor ID → consistent health metrics
- **Informer Status**: Seeded by informer ID or zone → predictable status

This enables reproducible testing and Phase 2 integration testing with real data sources.

## Logging

Structured JSON logging to stdout with fields:

- `timestamp` — ISO 8601 format
- `level` — INFO, WARNING, ERROR
- `logger` — Source component
- `message` — Log message
- `context` — Additional fields (tool name, input hash, etc.)

View logs:

```bash
# Local
dotnet run | jq .

# Docker
docker logs <container-id> | jq .
```

## Architecture

### Domain Modules

Each of the 4 domain modules:
- Validates input (required fields, value ranges)
- Generates deterministic stub data
- Logs requests and results
- Returns structured JSON response

### MCP Integration

- **ToolRegistry**: Maintains tool definitions and routes invocations to modules
- **McpController**: HTTP endpoint for `tools/list` and `tools/call` methods
- **HealthController**: Simple liveness probe

### Error Handling

- **400 Bad Request**: Invalid input (missing fields, invalid values)
- **404 Not Found**: Unknown tool name
- **500 Internal Server Error**: Unexpected errors (logged)

## Configuration

Environment variables:

| Variable | Default | Purpose |
|----------|---------|---------|
| `LISTEN_PORT` | 8000 | HTTP server port |
| `LOG_LEVEL` | INFO | Logging verbosity |
| `ASPNETCORE_URLS` | http://0.0.0.0:8000 | Kestrel binding address |

## Phase 2 Roadmap

- Replace stub data with real lightning detection APIs
- Add session management and multi-turn dialogue support
- Extend domain modules with time-series data (historical strikes, trend analysis)
- Add authentication and authorization
- Add PII redaction layer

## License

Apache 2.0
