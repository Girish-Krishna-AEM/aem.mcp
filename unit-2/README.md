# Coordinator Agent

A .NET 10.0 / C# microservice that accepts natural-language queries, classifies intent, extracts parameters, and invokes the Lightning MCP Server (Unit 1) on the user's behalf.

## Overview

The Coordinator Agent exposes a single HTTP endpoint for natural-language queries:

1. **Intent Classification** — pattern-based (regex keyword) classification into Strike, Weather, Sensor, or Informer
2. **Parameter Extraction** — regex-based extraction of location, radius, sensor_id, or zone
3. **MCP Tool Invocation** — HTTP call to the Lightning MCP Server's `/mcp/messages` endpoint, fail-fast (no retry)
4. **Result Formatting** — wraps the tool result with a natural-language summary

## Technology Stack

- **.NET 10.0** / **C#** / **ASP.NET Core**
- Pattern-based (regex) intent classification and parameter extraction — no LLM/SDK dependency in Phase 1
- Structured JSON logging to stdout

## Project Structure

```
unit-2/
├── src/
│   └── CoordinatorAgent/
│       ├── Program.cs                   # Startup configuration, DI setup
│       ├── CoordinatorAgent.csproj
│       ├── appsettings.json
│       ├── Dockerfile
│       ├── Models/
│       │   └── Intent.cs
│       ├── Services/
│       │   ├── IntentClassifier.cs
│       │   ├── ParameterExtractor.cs
│       │   ├── McpClient.cs
│       │   └── ResultFormatter.cs
│       └── Controllers/
│           ├── QueryController.cs       # POST /query
│           └── HealthController.cs      # GET /health
└── tests/
    └── CoordinatorAgent.Tests/
        ├── Services/                    # Unit tests
        └── Integration/                 # End-to-end tests
```

## Running Locally

### Prerequisites

- .NET 10.0 SDK
- The Lightning MCP Server (Unit 1) running and reachable (defaults to `http://lightning-mcp-server:8000`; override with `MCP_SERVER_URL` for standalone local runs, e.g. `http://localhost:8000`)

### Build

```bash
cd unit-2
dotnet build
```

### Run

```bash
cd unit-2/src/CoordinatorAgent
MCP_SERVER_URL=http://localhost:8000 dotnet run --environment Development
```

Server listens on `http://localhost:8001`.

### Test

```bash
cd unit-2
dotnet test
```

## Docker

Runs identically whether built/run on a local machine or on EC2 — see `aidlc-docs/construction/unit-2/infrastructure-design/deployment-architecture.md` for the shared `docker-compose.yml` service block and side-by-side local/EC2 instructions.

```bash
docker build -f unit-2/src/CoordinatorAgent/Dockerfile -t coordinator-agent:latest .
docker run -p 8001:8001 \
  -e MCP_SERVER_URL=http://host.docker.internal:8000 \
  -e LOG_LEVEL=INFO \
  coordinator-agent:latest
```

## API Endpoints

### Query

```bash
POST http://localhost:8001/query
Content-Type: application/json

{"query": "Is there lightning near Austin, TX?"}

# Response
{
  "toolName": "get_lightning_strikes_near_location",
  "result": {"strikeCount": 8, "strikes": [...], "nearestStrikeDistance": 3.45},
  "summary": "Found 8 lightning strike(s) with the nearest strike 3.45 km away."
}
```

More example queries: `aidlc-docs/construction/unit-2/code/intent-and-parameter-reference.md`

### Health Check

```bash
GET http://localhost:8001/health

# Response
{"status": "healthy", "timestamp": "2026-10-04T19:30:00Z", "mcp_server": "reachable"}
```

## Error Handling

- **400 Bad Request** — unclassifiable query or missing required parameters (message tells the user what's missing)
- **503 Service Unavailable** — MCP Server unreachable or timed out (no retry; fail-fast per NFR design)

## Configuration

| Variable | Default | Purpose |
|----------|---------|---------|
| `LISTEN_PORT` | 8001 | HTTP server port |
| `LOG_LEVEL` | INFO | Logging verbosity |
| `MCP_SERVER_URL` | http://lightning-mcp-server:8000 | Lightning MCP Server base URL |
| `ASPNETCORE_URLS` | http://0.0.0.0:8001 | Kestrel binding address |

## Troubleshooting

- **"Unable to determine what you're asking for"** — rephrase using a keyword like "lightning", "forecast", "sensor", or "status"/"device".
- **"Missing required parameter: latitude and longitude"** — include a known city (e.g., "Austin, TX") or explicit coordinates ("latitude 30.27, longitude -97.74").
- **503 MCP Server unavailable** — verify Unit 1 is running and `MCP_SERVER_URL` points to the correct host/port.

## Phase 2 Roadmap

- Replace pattern-based intent classification/parameter extraction with Claude SDK-based NLU
- Add session store and multi-turn dialogue manager for clarification
- Add retry/backoff policy for transient MCP Server failures
- Add caching for frequently extracted locations

## License

Apache 2.0
