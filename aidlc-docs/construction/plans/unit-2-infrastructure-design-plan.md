# Infrastructure Design Plan: Unit 2 — Coordinator Agent

## Context

Unit 1's infrastructure design (`aidlc-docs/construction/unit-1/infrastructure-design/infrastructure-design.md`) already established **shared infrastructure** for the Phase 1 POC: a single AWS EC2 `t3.small` instance (us-east-1, Amazon Linux 2023) running both containers side-by-side via Docker Compose — Lightning MCP Server (port 8000) and Coordinator Agent (port 8001) — on one bridge network, with no persistent storage, no auth, stdout JSON logging, and manual health checks.

Unit 2's NFR design (`aidlc-docs/construction/unit-2/nfr-design/logical-components.md`) confirms the Coordinator Agent is stateless, calls the MCP Server over HTTP via `MCP_SERVER_URL`, with a 10s call timeout / 2s health-probe timeout and no retry.

Because the compute, networking, container-runtime, observability, and security posture are already decided as **shared infrastructure**, this plan focuses only on the decisions specific to Unit 2 (or confirms inheritance from Unit 1).

## Plan Steps

- [ ] Step 1: Confirm compute/networking/runtime/observability/security are inherited unchanged from Unit 1's shared infrastructure (no new EC2, no new security group rules beyond the existing port 8001 rule already defined).
- [ ] Step 2: Resolve Unit-2-specific questions below (deployment env var wiring, inter-container dependency/startup order, health-check behavior, cost delta).
- [ ] Step 3: Generate `aidlc-docs/construction/unit-2/infrastructure-design/infrastructure-design.md`.
- [ ] Step 4: Generate `aidlc-docs/construction/unit-2/infrastructure-design/deployment-architecture.md` (Docker Compose service definition for `coordinator-agent`, extending Unit 1's compose file).
- [ ] Step 5: Present completion message and wait for approval.

## Context-Appropriate Questions

### Deployment Environment
Already decided by Unit 1 (same EC2 instance, same Docker Compose project). No new question.

### Compute Infrastructure
Already decided by Unit 1 (same `t3.small` instance hosts both containers; no separate compute needed for a stateless HTTP proxy service).
[Answer]: Confirmed — no separate compute.

### Storage Infrastructure
Coordinator Agent is stateless per NFR design (no session store in Phase 1).
[Answer]: Confirmed — no storage needed.

### Messaging Infrastructure
No async/event-driven pattern in NFR design (synchronous HTTP call to MCP Server only).
[Answer]: N/A — synchronous HTTP only.

### Networking Infrastructure
1. **Startup order**: Should Docker Compose enforce that `lightning-mcp-server` starts before `coordinator-agent` (e.g., via `depends_on` + healthcheck condition), or is independent startup acceptable given the Coordinator's fail-fast/no-retry error handling?
[Answer]: Use `depends_on` with `condition: service_healthy` so Coordinator doesn't start probing before MCP Server is ready.

2. **MCP_SERVER_URL value**: Confirm this resolves to the Docker Compose service name, i.e. `http://lightning-mcp-server:8000` (per NFR design example), not localhost or an external DNS name.
[Answer]: Confirmed — `http://lightning-mcp-server:8000` via Compose service DNS.

### Monitoring Infrastructure
Same as Unit 1 — stdout JSON logs via `docker compose logs`; manual `GET /health` checks. The Coordinator's `/health` endpoint additionally probes MCP Server connectivity (per NFR design) — confirm this is a "best-effort" indicator only, not a hard dependency gate.
[Answer]: Confirmed — best-effort indicator; does not block the Coordinator process from running.

### Shared Infrastructure
Confirm Unit 2 reuses Unit 1's Security Group (port 8001 already allowed inbound), EC2 instance, EBS volume, and Docker network — no modifications needed.
[Answer]: Confirmed — fully reused, no infrastructure changes beyond adding the `coordinator-agent` service block to the shared `docker-compose.yml`.
