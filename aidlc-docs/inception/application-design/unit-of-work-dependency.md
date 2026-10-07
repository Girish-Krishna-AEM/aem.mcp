# Unit of Work Dependencies: Lightning Detection MCP POC (Phase 1)

## Dependency Matrix

| From | To | Type | Contract | Notes |
|------|----|----|----------|-------|
| **Coordinator Agent (Unit 2)** | Lightning MCP Server (Unit 1) | Runtime (MCP over HTTP/SSE) | MCP tool schemas (inputs, outputs, error formats) | Loose coupling; Coordinator invokes one of 4 tools |
| **Coordinator Agent (Unit 2)** | LightningCommon (Shared) | Build-time (NuGet) | Logging API, health-check base classes | Both services depend |
| **Lightning MPC Server (Unit 1)** | LightningCommon (Shared) | Build-time (NuGet) | Logging API, health-check base classes | Both services depend |
| **Infrastructure Unit (Unit 3)** | Unit 1 (LightningMcpServer) | Build-time & Runtime | Docker image artifact, environment contracts | Unit 3 orchestrates Unit 1 container |
| **Infrastructure Unit (Unit 3)** | Unit 2 (CoordinatorAgent) | Build-time & Runtime | Docker image artifact, environment contracts | Unit 3 orchestrates Unit 2 container |
| **LightningCommon** | (None) | Standalone | — | No external dependencies for shared lib |

---

## Integration Contracts

### Contract 1: Coordinator Agent ↔ Lightning MCP Server (MCP Protocol)

**Transport Protocol**: HTTP/SSE MCP

**Service Discovery**:
- **Docker Compose DNS**: Coordinator reaches Lightning Server at hostname `lightning-mpc-server`, port 8000
- **Environment Variable**: `MPC_SERVER_URL=http://lightning-mpc-server:8000`
- **Fallback**: Localhost for local dev: `http://localhost:8000`
- **Timeout**: Connection 5 seconds, read 10 seconds

**MCP Tool Schemas** (Definitive Specification):

All tool specifications documented in `aidlc-docs/schemas/mcp-tools.json` (created in Code Generation phase).

**Tool 1: `get_lightning_strikes_near_location`**
- Input: location (City+State or ZIP), radius (numeric), radius_unit (miles|km)
- Output: location, radius, radius_unit, strike_count, strikes[], nearest_strike_distance
- Each strike: distance, timestamp (ISO 8601), intensity (0-100), latitude, longitude
- Errors: 400 for invalid input, 500 for server error

**Tool 2: `get_weather_forecast`**
- Input: location (City+State), forecast_type (daily|15-day)
- Output: location, forecast_type, forecast[]
- Each entry: date (YYYY-MM-DD), condition, temp_high, temp_low, precipitation_probability (0-100%), lightning_risk (Low|Medium|High)
- daily = 1 entry, 15-day = 15 entries
- Errors: 400 for invalid input

**Tool 3: `get_sensor_diagnostics`**
- Input: sensor_id (string, required)
- Output: sensor_id, detection_efficiency (0-100%), gps_visibility (Good|Fair|Poor), tracked_satellites, noise_level, uptime, last_calibration (ISO 8601), snr, status (Healthy|Degraded|Offline)
- Errors: 400 for missing sensor_id, 404 if not found

**Tool 4: `get_informer_status`**
- Input: informer_id OR zone (at least one required)
- Output: informer_id, zone, status (Active|Idle|Fault), last_activation (ISO 8601), power_status, device_type (Strobe|Horn|Combined)
- Errors: 400 if neither provided

**Error Handling Agreement**:
- MPC Server validates all inputs, returns well-formed errors
- Error response format: `{"error": "message", "details": "..."}`
- Coordinator catches connection errors, retries (3 attempts, exponential backoff 1s/2s/4s)
- Coordinator logs errors and returns user-friendly message

**Backward Compatibility Rules**:
- New tools can be added (registry-based discovery)
- New optional fields can be added to outputs
- Existing required fields must not change meaning
- No breaking changes to error response format

---

### Contract 2: Services ↔ LightningCommon (Shared Library)

**Dependency Type**: Build-time (project reference or NuGet package)

**Provided Components**:
1. Structured logging (ILogger with JSON formatter)
2. Health-check endpoints (`GET /health` → 200 `{"status": "healthy"}`)
3. Common error response models
4. Dependency injection extensions

**Usage** (in Unit 1/2 Program.cs):
```csharp
services.AddLightningCommon();
app.UseStructuredLogging();
app.UseHealthCheckEndpoint("/health");
```

---

### Contract 3: Services ↔ Infrastructure (Docker & EC2)

**Dockerfile Contracts**:

**Lightning MPC Server**:
- Exposes port 8000 (HTTP/SSE)
- Health-check CMD: `curl -f http://localhost:8000/health`
- Environment variables: ASPNETCORE_URLS, LOG_LEVEL, ASPNETCORE_ENVIRONMENT

**Coordinator Agent**:
- Exposes port 5000 (HTTP)
- Health-check CMD: `curl -f http://localhost:5000/health`
- Environment variables: ASPNETCORE_URLS, MPC_SERVER_URL, LOG_LEVEL, ASPNETCORE_ENVIRONMENT, CLAUDE_API_KEY

**Health-Check Contract** (docker-compose.yml):
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:<PORT>/health"]
  interval: 10s
  timeout: 5s
  retries: 3
  start_period: 5s
```

**Startup Ordering**:
- Coordinator waits for Lightning MPC Server health-check to pass (depends_on: condition: service_healthy)

**Networking**: Both services on docker network `lightning-network`; Docker DNS resolves service names automatically

---

## Deployment Independence

### Phase 1: NO Independent Deployment

- Single docker-compose.yml orchestrates both
- Coordinator starts only after MPC Server healthy
- No explicit versioning strategy
- Treat as single deployment unit

### Phase 2+: YES (If backward-compatible MCP schemas)

- Explicit versioning with compatibility matrix
- Coordinator can gracefully handle missing tools or schema differences
- Independent release cycles

---

## Testing Strategy

**Unit Tests** (per service):
- Unit 1: Test each domain module independently
- Unit 2: Test intent classifier, parameter extractor, mocked MPC client
- Scope: Function-level, no external dependencies

**Integration Tests** (cross-service):
- Start docker-compose
- Submit queries to Coordinator
- Verify end-to-end routing and responses
- Test error handling (MPC Server unavailable, timeout, invalid responses)

**End-to-End Tests** (full stack):
- docker-compose up, verify health-checks pass
- Submit 4 representative queries (one per tool)
- Verify logs are structured JSON
- Verify no errors or warnings

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| MCP Protocol HTTP/SSE implementation challenges | Services cannot communicate | Early spike with MCP SDK; defer to stdio if blocking |
| Service discovery (docker DNS) fails | Coordinator cannot reach MPC Server | Use hardcoded IP for local dev; docker-compose DNS for prod |
| LLM intent classification low confidence | Poor user experience | Phase 1 includes confidence scoring; Coordinator clarifies |
| Shared library version mismatch | Incompatible versions | Monorepo ensures single version; explicit versioning Phase 2 |
| Health-check timing issues | Coordinator starts before MPC Server ready | Generous timeouts (10s interval, 3 retries, 5s grace) |
| Docker image bloat | Long build/push times | Multi-stage builds; optimize base images |
| Environment variable secrets leaked | CLAUDE_API_KEY exposed | Use .env file (not in git); Phase 2 uses Secrets Manager |

---

## Change Control

### Breaking Changes (Require Phase 2+ Planning)
- MCP tool schema changes: New required inputs, removed outputs, changed types
- Docker Compose port changes
- Environment variable contract changes
- Health-check endpoint changes
- Logging format breaking changes

### Non-Breaking Changes (Safe for Phase 1)
- Adding new MCP tools
- Adding new optional output fields
- Adding new optional environment variables (with defaults)
- Internal refactoring of domain modules
- Improving logging detail level
- Coordinator intent tuning

---

**Dependencies fully specified for hand-off to Code Generation.**
