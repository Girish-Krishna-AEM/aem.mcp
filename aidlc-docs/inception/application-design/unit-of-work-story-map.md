# Unit of Work Story Map: Lightning Detection MCP POC (Phase 1)

## Functional Requirements Mapping

### FR-1: Lightning MCP Server (4 MCP Tools) — Unit 1

| FR | Tool | Acceptance Criteria | Unit |
|:--:|:----:|:------------------:|:----:|
| **FR-1.1** | `get_lightning_strikes_near_location` | Accepts location, radius, radius_unit; validates inputs; returns strike_count, strikes[], nearest_strike_distance; stub data deterministic | ✅ Unit 1 |
| **FR-1.2** | `get_weather_forecast` | Accepts location, forecast_type; daily=1 entry, 15-day=15 entries; returns date, condition, temp, precipitation %, lightning_risk | ✅ Unit 1 |
| **FR-1.3** | `get_sensor_diagnostics` | Accepts sensor_id; returns detection_efficiency, gps_visibility, tracked_satellites, noise_level, uptime, last_calibration, snr, status | ✅ Unit 1 |
| **FR-1.4** | `get_informer_status` | Accepts informer_id OR zone; returns status, last_activation, power_status, zone, device_type; validates at least one input | ✅ Unit 1 |

### FR-2: Coordinator Agent — Unit 2

| Component | Responsibility | Acceptance Criteria | Unit |
|:----------:|:---------------:|:------------------:|:----:|
| **Intent Classification** | Parse NL query → intent (Strikes, Weather, Sensor, Informer) | Correctly classifies representative queries with ≥70% confidence; asks for clarification if uncertain | ✅ Unit 2 |
| **Parameter Extraction** | Extract location, radius, sensor_id, zone from query | Returns required parameters for tool invocation; asks for missing params | ✅ Unit 2 |
| **MCP Tool Invocation** | Invoke correct tool on Lightning MPC Server | Routes to correct tool based on intent; handles errors gracefully; retries on transient failures | ✅ Unit 2 |
| **Result Formatting** | Format tool output for user | Extracts key fields; optionally wraps with natural-language summary | ✅ Unit 2 |
| **Missing Parameter Handling** | Ask user for required missing parameters | Simple one-turn clarification; specific, not generic messages | ✅ Unit 2 |

### FR-3: Interfaces — Unit 1 & 2

| Interface | Component | Acceptance Criteria | Units |
|:---------:|:---------:|:------------------:|:-----:|
| **MCP Protocol Exposure** | Lightning MPC Server | Exposes 4 tools via HTTP/SSE on port 8000; tool schemas well-defined | ✅ Unit 1 |
| **HTTP Endpoint** | Coordinator Agent | POST /query accepts `{"query": "..."}`, returns tool result + optional framing on port 5000 | ✅ Unit 2 |

---

## Non-Functional Requirements Mapping

| NFR | Requirement | Unit Owner(s) | Acceptance Criteria |
|:---:|:-----------:|:--------------:|:------------------:|
| **NFR-1** | Technology Stack: .NET 10.0, C# | Unit 1 & 2 | Both services implemented in .NET 10.0 / C# |
| **NFR-2.1** | Docker Images | Unit 3 + 1 & 2 | Both services have multi-stage Dockerfiles, health-checks, reasonable size |
| **NFR-2.2** | Docker Compose Orchestration | Unit 3 | docker-compose.yml orchestrates both services, startup order correct, networking configured |
| **NFR-2.3** | EC2 Deployment | Unit 3 | Instance type recommendations (t3.small/medium), AMI (Amazon Linux 2023 or Ubuntu LTS), security group rules documented |
| **NFR-3.1** | Structured Logging | Unit 1 & 2 & 3 | Both services emit structured JSON logs; logs visible via `docker-compose logs`; startup, request, result, error messages logged |
| **NFR-3.2** | Health-Check Endpoints | Unit 1 & 2 & 3 | Both expose GET /health (HTTP 200 `{"status": "healthy"}`); docker-compose configured with health-checks (10s interval, 5s timeout, 3 retries) |

---

## Acceptance Criteria Mapping

| AC # | Description | Unit(s) | Status |
|:----:|:-----------:|:-------:|:------:|
| **AC#1** | `docker-compose up` starts both containers, both health-checks pass | Unit 1 & 2 & 3 | ✅ |
| **AC#2** | All 4 MPC tools return well-formed JSON (no parse errors, valid schemas) | Unit 1 | ✅ |
| **AC#3** | Coordinator routes representative queries to correct tools (verified per tool type) | Unit 2 | ✅ |
| **AC#4** | Missing required parameters handled gracefully (ask user, don't crash) | Unit 2 | ✅ |
| **AC#5** | Logs visible via `docker-compose logs` (structured JSON format, no ERROR level exceptions) | Unit 1 & 2 & 3 | ✅ |
| **AC#6** | No authentication, authorization, PII redaction, or edge controls present (Phase 1 scope) | Unit 1 & 2 | ✅ |

---

## Work Breakdown Structure (27 Stories)

### Unit 1: Lightning MCP Server (11 Stories)

1. **Setup MPC Server Project** — .NET 10 project, Program.cs, appsettings.json, MCP SDK, ASP.NET, DI, logging
2. **Implement Strike Detection Module** — get_lightning_strikes_near_location tool
3. **Implement Weather Forecast Module** — get_weather_forecast tool
4. **Implement Sensor Diagnostics Module** — get_sensor_diagnostics tool
5. **Implement Informer Status Module** — get_informer_status tool
6. **Tool Registration & MCP Integration** — Register all 4 tools, HTTP/SSE transport
7. **Error Handling** — Validation errors, timeout handling
8. **Unit Tests** — Test each module independently
9. **Create Dockerfile** — Multi-stage build, health-check CMD
10. **Documentation** — Module responsibilities, integration guide
11. **Integration Tests** — Test MPC Server independently

### Unit 2: Coordinator Agent (11 Stories)

1. **Setup Coordinator Project** — .NET 10 project, ASP.NET Core, DI, logging
2. **Implement Intent Classifier** — LLM-based intent classification (Claude SDK)
3. **Implement Parameter Extractor** — Extract location, radius, sensor_id, zone
4. **Implement MPC Client Wrapper** — HTTP/SSE MCP client with error handling & retry
5. **Implement Result Formatter** — Format tool output for user
6. **Implement Missing Parameter Handler** — Simple one-turn clarification
7. **Create /query HTTP Endpoint** — POST /query handler
8. **Integration with MPC Server** — Test Coordinator → MPC Server flow
9. **Unit Tests** — Test intent classifier, parameter extractor, mocked client
10. **Create Dockerfile** — Multi-stage build, health-check CMD
11. **Documentation** — Configuration guide, example queries, troubleshooting

### Unit 3: Infrastructure Orchestration (5 Stories)

1. **Create docker-compose.yml** — Orchestrate both services, networking, health-checks, startup ordering
2. **Create .env.example** — Environment variable template
3. **Create EC2 Deployment Guide** — Instance type, AMI, security group, startup
4. **Create DEPLOYMENT.md** — Comprehensive deployment documentation
5. **Test Local docker-compose** — Build images, run stack, verify health-checks, submit queries, verify logs

---

## Summary Table

| Requirement Type | Component | Unit(s) | Assigned | Status |
|:---------------:|:---------:|:-------:|:--------:|:------:|
| **FR-1.1** | Strike Tool | Unit 1 | ✅ | ✅ |
| **FR-1.2** | Weather Tool | Unit 1 | ✅ | ✅ |
| **FR-1.3** | Sensor Tool | Unit 1 | ✅ | ✅ |
| **FR-1.4** | Informer Tool | Unit 1 | ✅ | ✅ |
| **FR-2** | Coordinator Agent | Unit 2 | ✅ | ✅ |
| **FR-3** | Interfaces (MCP + HTTP) | Unit 1 & 2 | ✅ | ✅ |
| **NFR-1** | Tech Stack (.NET 10/C#) | Unit 1 & 2 | ✅ | ✅ |
| **NFR-2** | Containerization & EC2 | Unit 3 | ✅ | ✅ |
| **NFR-3** | Logging & Health Checks | Unit 1 & 2 & 3 | ✅ | ✅ |
| **AC#1–6** | Acceptance Criteria | All | ✅ | ✅ |

**All functional requirements, non-functional requirements, and acceptance criteria assigned with no gaps or overlaps.**

---

## Phase 1 → Phase 2+ Evolution

**Phase 2**: Replace stubs with real data sources; add Session Store; add Authentication.
- Domain module split: 4 domain-specific MPC Servers (Strike, Weather, Sensor, Informer)
- Coordinator extended: Multi-turn dialogue, session routing, result aggregation

**Phase 3**: Complete reference architecture (Edge Layer, PII Redaction, Observability, Cost Tracker, Agent Evaluation Suite).

---

**Story map complete. Ready for Code Generation phase.**
