# Integration Test Instructions

## Purpose

Test the interaction between Unit 1 (Lightning MCP Server) and Unit 2 (Coordinator Agent) end-to-end, beyond each unit's own in-process integration tests (which fake the other side of the HTTP boundary).

## Test Scenarios

### Scenario 1: Coordinator Agent → Lightning MCP Server (real HTTP, both services running)

- **Description**: Confirm the Coordinator Agent's `McpClient` can actually reach the real Unit 1 service over HTTP (not a faked handler), for all 4 intents.
- **Setup**: Both containers running via `docker compose up -d` (see `DEPLOYMENT.md`)
- **Test Steps**:
  1. `POST /query` to Coordinator Agent with a Strike-intent query (e.g., "Is there lightning near Austin, TX?")
  2. `POST /query` with a Weather-intent query (e.g., "What's the weather in Dallas?")
  3. `POST /query` with a Sensor-intent query (e.g., "Show diagnostics for sensor-001")
  4. `POST /query` with an Informer-intent query (e.g., "Is the horn in zone-a powered on?")
- **Expected Results**: Each returns HTTP 200 with `{"toolName": ..., "result": ..., "summary": ...}`, and `result` matches the shape documented in `aidlc-docs/construction/unit-1/code/mcp-tool-schemas.md`
- **Cleanup**: `docker compose down` (or leave running if continuing to Scenario 2)

### Scenario 2: MCP Server Unavailable → Coordinator Fails Fast

- **Description**: Confirm the Coordinator Agent returns HTTP 503 (not a hang or 500) when the MCP Server is down.
- **Setup**: Start only `coordinator-agent` without `lightning-mcp-server` (`docker compose up -d coordinator-agent` — note: `depends_on: condition: service_healthy` means Compose will refuse to start Coordinator alone without its dependency being healthy; to test true unavailability, start both, then `docker compose stop lightning-mcp-server`)
- **Test Steps**: `POST /query` with any valid query after stopping `lightning-mcp-server`
- **Expected Results**: HTTP 503 with `{"error": "MCP Server is currently unavailable. Please try again in a moment."}`
- **Cleanup**: `docker compose start lightning-mcp-server` or `docker compose down`

### Scenario 3: Health Check Reflects MCP Connectivity

- **Description**: Confirm Coordinator's `/health` endpoint correctly reports MCP Server reachability.
- **Setup**: Both services running
- **Test Steps**: `curl http://localhost:8001/health` with MCP Server up, then again after `docker compose stop lightning-mcp-server`
- **Expected Results**: `"mcp_server": "reachable"` → `"mcp_server": "unreachable"`; Coordinator's own `"status": "healthy"` is unaffected either way (best-effort probe, not a hard dependency gate, per NFR design)
- **Cleanup**: `docker compose start lightning-mcp-server`

## Setup Integration Test Environment

### 1. Start Required Services

```bash
docker compose up -d --build
```

### 2. Configure Service Endpoints

No manual configuration needed — `MCP_SERVER_URL` is pre-wired to the Compose service DNS name in `docker-compose.yml`/`.env.example`.

## Run Integration Tests

### 1. Execute Integration Test Suite

Each unit's own `dotnet test` run already includes in-process integration tests against a faked boundary (see `unit-test-instructions.md`). The cross-unit scenarios above (real HTTP between both running containers) are manual `curl` scenarios — there is no automated cross-container test harness in this Phase 1 POC.

```bash
# Scenario 1 (repeat for each intent)
curl -X POST http://localhost:8001/query -H "Content-Type: application/json" -d '{"query": "Is there lightning near Austin, TX?"}'
```

### 2. Verify Service Interactions

- **Test Scenarios**: See the 3 scenarios above
- **Expected Results**: See each scenario's "Expected Results"
- **Logs Location**: `docker compose logs -f` (both services emit structured JSON)

### 3. Cleanup

```bash
docker compose down
```

## Known Limitation (This Session)

These cross-container scenarios require a running Docker engine and were **not executed in this authoring session** — Docker Desktop's engine was unavailable here (see `aidlc-docs/construction/unit-3/code/local-compose-test-results.md`). Each unit's in-process integration tests (which exercise the same request/response logic against a faked HTTP boundary) **were** run and passed (25/25 and 29/29). Run the scenarios above on a machine with Docker running to complete full cross-service verification before EC2 deployment.
