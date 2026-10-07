# NFR Design Plan: Unit 1 - Lightning MCP Server

## Context
Unit 1 (Lightning MCP Server) is a standalone .NET 10 service exposing 4 MCP tools via HTTP/SSE transport. This plan identifies design patterns and logical components needed to incorporate NFRs into the architecture.

## Identified NFRs to Address

From `requirements.md`:
- **NFR-1**: Technology Stack (.NET 10.0, C#)
- **NFR-2**: Containerization & Deployment (Docker, Docker Compose, single EC2)
- **NFR-3**: Observability (basic structured logs, health checks)
- **NFR-5**: Maintainability/Extensibility (pluggable stub logic, extensible for Phase 2 data sources)
- **NFR-6**: Performance (POC-level, sub-second response time)

## NFR Design Questions

### 1. **Resilience Patterns** — Fault Tolerance & Recovery

**Question 1.1**: Should the Lightning MCP Server implement retry logic for internal module failures (e.g., if one of the 4 domain modules encounters a transient error), or should failures be immediately returned to the caller as error responses?

**[Answer 1.1]**:

**Question 1.2**: For a POC, do you want graceful degradation (e.g., return a partial/degraded response even if one module fails), or immediate failure propagation? This affects whether we need circuit breaker patterns or fallback logic.

**[Answer 1.2]**:

**Question 1.3**: Should the service implement a graceful shutdown pattern (wait for in-flight requests to complete before terminating), or immediate shutdown on container stop signal? This matters for Docker Compose orchestration reliability.

**[Answer 1.3]**:

---

### 2. **Scalability Patterns** — Load & Growth

**Question 2.1**: For Phase 1 (POC), are you assuming a single instance of the Lightning MCP Server running at a time, or should the design anticipate that multiple instances might run behind a load balancer (even if Phase 1 deployment doesn't use it)?

**[Answer 2.1]**:

**Question 2.2**: Should the service maintain any in-memory state (e.g., cache of recent tool responses, request counters for monitoring), or should it be completely stateless? Stateless is simpler; stateful enables caching optimization for Phase 2.

**[Answer 2.2]**:

---

### 3. **Performance Patterns** — Optimization & Latency

**Question 3.1**: Given that all tool responses are stubbed/synthetic in Phase 1 (no real I/O), are sub-second response times sufficient, or is there a specific latency target you'd like to enforce (e.g., <100ms, <500ms)?

**[Answer 3.1]**:

**Question 3.2**: Should the stub data generators (strike locations, weather forecasts, sensor diagnostics) use deterministic seeding (same input → same output for reproducible testing), or randomized generation on each call? Deterministic is better for testing.

**[Answer 3.2]**:

**Question 3.3**: For observability during the POC, should structured logging be at DEBUG level (verbose, all tool invocations logged), INFO level (startup, major operations only), or ERROR level (errors only)?

**[Answer 3.3]**:

---

### 4. **Security Patterns** — Protection & Compliance

**Question 4.1**: For Phase 1, no input authentication/authorization is required per requirements. However, should the service validate tool inputs at the MCP level (e.g., reject empty strings, negative radius values, malformed JSON) before passing to domain modules?

**[Answer 4.1]**:

**Question 4.2**: Should tool responses be sanitized before returning to the caller (e.g., ensure all JSON values are properly escaped), or is raw output acceptable for a POC?

**[Answer 4.2]**:

---

### 5. **Logical Components & Infrastructure** — Architecture Elements

**Question 5.1**: The service needs a **Health Check Endpoint** (NFR-3). Should this be a simple HTTP GET `/health` returning `{"status": "healthy"}`, or do you want it to perform deeper checks (e.g., verify all 4 domain modules are loaded/responsive before returning healthy)?

**[Answer 5.1]**:

**Question 5.2**: Should the service implement a **structured logging framework** (e.g., JSON-formatted logs with request/response IDs for tracing), or simple console text logs? Structured is more maintainable and Phase-2-friendly.

**[Answer 5.2]**:

**Question 5.3**: For the MCP protocol transport (FR-3), the requirements say "stdio or HTTP/SSE — implementer's choice." Given Docker Compose will orchestrate the services, which transport do you prefer: **HTTP/SSE** (easier for inter-container communication via network) or **stdio** (simpler for single-machine POC)?

**[Answer 5.3]**:

**Question 5.4**: Should the service read configuration from environment variables (e.g., `LISTEN_PORT`, `LOG_LEVEL`), hardcoded defaults, or a config file? Environment variables are Docker-friendly and Phase-2-extensible.

**[Answer 5.4]**:

---

## Summary of Design Plan Checkboxes

- [ ] **1.1–1.3**: Define resilience patterns (retry logic, degradation, graceful shutdown)
- [ ] **2.1–2.2**: Define scalability patterns (single/multi-instance, stateless/stateful)
- [ ] **3.1–3.3**: Define performance patterns (latency targets, deterministic stubs, logging level)
- [ ] **4.1–4.2**: Define security patterns (input validation, output sanitization)
- [ ] **5.1–5.4**: Design logical components (health check, logging framework, MCP transport, configuration strategy)
- [ ] Generate `nfr-design-patterns.md` with selected patterns
- [ ] Generate `logical-components.md` with detailed component definitions
- [ ] Present completion message and wait for approval

---

**Next Step**: Please answer all questions above using the `[Answer N.N]` tags. Once collected, I'll analyze responses, generate NFR design artifacts, and present them for your review.
