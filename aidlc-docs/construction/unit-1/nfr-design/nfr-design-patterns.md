# NFR Design Patterns: Unit 1 — Lightning MCP Server

## Overview

This document specifies the non-functional design patterns incorporated into Unit 1 (Lightning MCP Server) based on the approved NFRs from requirements.md and the critical design decisions made during NFR Design planning.

---

## 1. Resilience Patterns

### 1.1 Fail-Fast Strategy
- **Pattern**: Errors in domain modules (strike generation, forecast, sensor diagnostics, informer status) are immediately returned to the caller as structured error responses.
- **Rationale**: For a POC with synthetic/stubbed data, retry logic adds unnecessary complexity. Failures are deterministic (e.g., invalid input), not transient.
- **Implication**: No circuit breaker, bulkhead isolation, or exponential backoff in Phase 1. Phase 2 can introduce resilience patterns if real data sources (APIs, databases) are added.

### 1.2 Graceful Shutdown
- **Pattern**: On container stop signal (SIGTERM), the service:
  1. Stops accepting new HTTP requests (HTTP listener pauses)
  2. Allows in-flight requests up to a timeout (e.g., 10 seconds) to complete
  3. Logs shutdown event
  4. Exits cleanly
- **Rationale**: Prevents abrupt termination mid-request; Docker Compose respects graceful shutdown for orchestration.
- **Implementation**: ASP.NET Core's hosted service pattern handles this via `IHostApplicationLifetime`.

---

## 2. Scalability Patterns

### 2.1 Stateless Service Design
- **Pattern**: The Lightning MCP Server maintains **zero persistent state** across requests. Each tool invocation is independent and reproducible.
- **Rationale**: 
  - Phase 1 is a single-instance POC; no state synchronization needed.
  - Deterministic stub generation (seeded by input) ensures reproducibility without caching.
  - Supports horizontal scaling in Phase 2 without session affinity or distributed cache.
- **Implication**: No in-memory cache, request counters, or session tables. All state is derived from request inputs.

### 2.2 Stateless HTTP/SSE Transport
- **Pattern**: MCP protocol implemented over HTTP/SSE (Server-Sent Events), with each tool call as an independent HTTP POST or WebSocket message.
- **Rationale**: 
  - HTTP is container-network-friendly (DNS resolution, load-balancer compatible).
  - SSE (for streaming responses) supports future multi-turn interactions without redesign.
  - Compared to stdio: avoids process stdin/stdout complexity, enables inter-container networking.
- **Implication**: MCP SDK is configured to use HTTP/SSE transport (not stdio).

---

## 3. Performance Patterns

### 3.1 Deterministic Stub Generation
- **Pattern**: All synthetic data (strike locations, weather forecasts, sensor diagnostics, device statuses) are generated using **deterministic seeding** based on input parameters.
  - Example: `get_lightning_strikes_near_location(location="Austin, TX", radius=10)` always produces the same N strikes with the same offsets, timestamps, and intensities.
  - Implementation: Stub generators use `System.Random(hashCode(input))` or similar seeding strategy.
- **Rationale**: 
  - Enables reproducible testing and debugging ("I got strikes X, Y, Z before, and I get them again now").
  - Simplifies test assertions (expected values don't change between runs).
  - Supports Phase 2 integration testing (mock vs. real data source comparison).
- **Implication**: No `Guid.NewGuid()` or `DateTime.Now` injected randomness in stub outputs; use seeded pseudo-random generation instead.

### 3.2 Sub-Second Latency (POC-level)
- **Pattern**: All tool responses complete in <1 second (typically <100ms), since no real I/O (network calls, database queries, file I/O) is performed.
- **Rationale**: POC requirement; acceptable for proof-of-concept validation. Phase 2 may add SLA targets (e.g., p99 <500ms) if real data sources introduce latency.
- **Implication**: No caching layer or query optimization needed in Phase 1. Monitoring for latency degradation is deferred to Phase 2 observability layer.

### 3.3 Structured JSON Logging at INFO Level
- **Pattern**: All significant events (service startup, MCP tool received, tool result returned, errors) are logged as structured JSON with:
  - Timestamp (ISO 8601)
  - Log level (INFO, ERROR, DEBUG)
  - Message/event description
  - Request/Response metadata (optional: tool name, input summary, output size, elapsed time)
- **Rationale**: 
  - Structured logs are machine-parseable (`docker compose logs | jq`), enabling automated analysis.
  - INFO level provides visibility without excessive verbosity.
  - Phase 2 can ingest these logs into observability systems (ELK, CloudWatch, Splunk).
- **Implication**: Use JSON-structured logging library (e.g., Serilog in .NET) with console sink; all console output is structured JSON, not free-form text.

---

## 4. Security Patterns

### 4.1 Input Validation at MCP Tool Level
- **Pattern**: Each domain module validates its inputs before processing:
  - `get_lightning_strikes_near_location`: Validate location is non-empty, radius is positive.
  - `get_weather_forecast`: Validate location is non-empty, forecast_type is 'daily' or '15-day'.
  - `get_sensor_diagnostics`: Validate sensor_id is non-empty.
  - `get_informer_status`: Validate at least one of informer_id or zone is provided and non-empty.
- **Rationale**: 
  - Prevents invalid inputs from propagating into stub generation.
  - Returns clear error messages to the caller (e.g., "Missing required parameter: location").
  - Phase 1 validation is simple; Phase 2 can add schema validation, type coercion, and boundary checks.
- **Implication**: Each domain module includes a validation step before stub generation. Validation errors return a structured error response.

### 4.2 Output Sanitization
- **Pattern**: All JSON responses are validated for well-formedness before returning to the caller:
  - Ensure all strings are properly JSON-escaped (quotes, newlines, special characters).
  - Ensure numeric values are valid (no NaN, Infinity).
  - Ensure arrays/objects are well-formed.
- **Rationale**: 
  - Prevents JSON injection or malformed output.
  - Ensures Coordinator Agent can parse and forward responses without error.
- **Implication**: Use .NET's `System.Text.Json` serialization (which handles escaping automatically), and validate before returning. No custom string concatenation for JSON assembly.

---

## 5. Configuration & Extensibility Patterns

### 5.1 Environment Variable Configuration
- **Pattern**: Runtime configuration is read from environment variables:
  - `LISTEN_PORT` (default: 8000) — Port the MCP Server listens on
  - `LOG_LEVEL` (default: INFO) — Structured log verbosity
  - `MCP_PROTOCOL` (default: http-sse) — Transport protocol (for future flexibility)
- **Rationale**: 
  - Docker-native; container environments (Docker Compose, Kubernetes) set env vars at runtime.
  - Supports Phase 2 deployment variations (different EC2 instances, regions) without code changes.
- **Implication**: ASP.NET Core configuration builder reads from `IConfiguration` (which includes env vars by default).

### 5.2 Pluggable Domain Module Architecture
- **Pattern**: Each of the 4 domain modules (Strike Detection, Weather Forecast, Sensor Diagnostics, Informer Status) is implemented as a pluggable class:
  - Modules implement a common `ILightningTool` interface.
  - MCP Tool Registry discovers and registers modules at startup.
  - Modules are dependency-injected (logging, config) so they're testable in isolation.
  - Stub data generation logic is encapsulated within each module; replacing stub logic with real API calls in Phase 2 requires only rewriting the module's logic, not the MCP layer.
- **Rationale**: 
  - Enables modular testing (each module tested independently).
  - Supports Phase 2 extraction to separate services (microservices refactor) with minimal changes.
  - Follows SOLID principles (single responsibility, dependency inversion).
- **Implication**: Domain modules are discoverable via reflection or explicit registration; Tool Registry handles routing.

---

## Summary of Design Patterns

| NFR Dimension | Pattern | Key Benefit |
|---|---|---|
| **Resilience** | Fail-fast, simple graceful shutdown | POC simplicity; no premature resilience complexity |
| **Scalability** | Stateless design; HTTP/SSE transport | Supports single-instance POC and future multi-instance scaling |
| **Performance** | Deterministic stubs; sub-second latency; INFO-level JSON logs | Reproducible testing, fast response, observable |
| **Security** | Input validation; output sanitization | Basic hygiene; prevents injection and malformed responses |
| **Extensibility** | Environment variable config; pluggable domain modules | Phase 2 integration-ready (replace stubs with real APIs, extract to microservices) |

---

**Next**: See `logical-components.md` for detailed component architecture and implementation patterns.
