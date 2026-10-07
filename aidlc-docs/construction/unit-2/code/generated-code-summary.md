# Generated Code Summary: Unit 2 — Coordinator Agent

## Overview

Unit 2 implements a stateless HTTP coordinator that translates natural-language queries into Lightning MCP Server (Unit 1) tool invocations, following the approved NFR design (pattern-based intent/parameter processing, fail-fast MCP client, single-turn clarification) and infrastructure design (environment-agnostic Docker Compose — runs unchanged locally or on EC2).

## Architecture

```
QueryController / HealthController   (HTTP layer)
        │
        ▼
IIntentClassifier  →  IParameterExtractor   (regex-based NLU)
        │
        ▼
IMcpClient   (HttpClient → Unit 1 /mcp/messages, 10s timeout, no retry)
        │
        ▼
ResultFormatter   (tool result + natural-language summary)
```

## Generated Artifacts

### Application Code (`unit-2/src/CoordinatorAgent/`)
- `Program.cs` — DI, logging, Kestrel, named `HttpClient` configuration (`MCP_SERVER_URL`)
- `CoordinatorAgent.csproj`, `appsettings.json`, `appsettings.Development.json`, `Dockerfile`
- `Models/Intent.cs` — Strike/Weather/Sensor/Informer enum
- `Services/IntentClassifier.cs` — regex keyword classification
- `Services/ParameterExtractor.cs` — regex-based location/radius/sensorId/informerId/zone extraction with a known-city lookup table
- `Services/McpClient.cs`, `McpServerException.cs` — HTTP client wrapper, fail-fast error mapping
- `Services/ResultFormatter.cs` — per-intent natural-language summaries
- `Controllers/QueryController.cs` — `POST /query`
- `Controllers/HealthController.cs` — `GET /health` (best-effort MCP connectivity probe)

### Tests (`unit-2/tests/CoordinatorAgent.Tests/`)
- `Services/IntentClassifierTests.cs` — 9 tests across all 4 intents + unrecognized query
- `Services/ParameterExtractorTests.cs` — 9 tests covering success/default/error paths per intent
- `Services/McpClientTests.cs` — 6 tests (success, 400, 500, network failure, health success/failure) using a fake `HttpMessageHandler`
- `Integration/CoordinatorIntegrationTests.cs` — 5 end-to-end tests via `WebApplicationFactory<Program>` with the MCP Server's HTTP client swapped for a fake handler

### Documentation
- `unit-2/README.md` — build/run/Docker/API/troubleshooting
- `aidlc-docs/construction/unit-2/code/intent-and-parameter-reference.md` — keyword tables, regex patterns, example mappings

## Design Notes

- **No LLM/SDK dependency**: Intent classification and parameter extraction are pure regex, per the explicit NFR Design decision (overriding the original story-map's "LLM-based" wording).
- **Fail-fast MCP Client**: No retry logic; network/timeout failures map directly to HTTP 503 with a clear user-facing message.
- **Local/EC2 parity**: All configuration (port, log level, MCP Server URL) is environment-variable driven with no hardcoded EC2 values, satisfying the infrastructure design's local-development flexibility requirement.
- **Stateless**: No session store; each request is independent (Phase 2 roadmap item).

## Next Phase

Build & Test stage — compile both units, run all unit/integration tests, and verify the end-to-end flow (Coordinator → MCP Server) via Docker Compose.
