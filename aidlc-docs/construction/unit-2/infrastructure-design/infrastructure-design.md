# Infrastructure Design: Unit 2 — Coordinator Agent

## Overview

Unit 2 (Coordinator Agent) reuses 100% of the shared infrastructure already established in Unit 1's infrastructure design (`aidlc-docs/construction/unit-1/infrastructure-design/infrastructure-design.md`). No new AWS resources, networking, or runtime infrastructure are introduced. This document records the few decisions specific to Unit 2 and points back to Unit 1 for everything else.

---

## 1. Compute Infrastructure: Inherited

**Decision**: No new compute. Coordinator Agent runs as a second container (`coordinator-agent`) on the same EC2 `t3.small` instance (us-east-1, Amazon Linux 2023) as the Lightning MCP Server.

**Rationale**: Stateless HTTP proxy service with no real workload in Phase 1 (synthetic data via MCP Server); the existing instance has sufficient headroom (sized for exactly this two-container footprint — see Unit 1 doc, EBS breakdown).

---

## 2. Storage Infrastructure: None

**Decision**: No persistent storage. Coordinator Agent is stateless (no session store, no cache) per NFR design.

---

## 3. Networking Infrastructure

### Security Group
**Decision**: Reuse Unit 1's security group. Port 8001 (Coordinator Agent, HTTP) is already defined as an inbound rule in Unit 1's infrastructure design — no change required.

### Inter-Container Communication
**Decision**: `MCP_SERVER_URL=http://lightning-mcp-server:8000`, resolved via Docker Compose's built-in service-name DNS on the shared bridge network (no manual DNS or `extra_hosts` configuration).

### Startup Ordering
**Decision**: `docker-compose.yml` sets `coordinator-agent` to `depends_on: lightning-mcp-server` with `condition: service_healthy`, gated on Unit 1's existing container healthcheck. This avoids the Coordinator attempting MCP calls before the server is ready, consistent with the Coordinator's fail-fast/no-retry error handling (NFR design: no retry on MCP Server errors).

**Note**: This is a startup convenience only — per NFR design, the Coordinator's `/health` endpoint treats MCP Server connectivity as a best-effort signal, not a hard dependency; the Coordinator process itself starts and serves `/health` regardless of MCP Server state after initial startup.

---

## 4. Container Runtime Infrastructure

**Decision**: Reuse Unit 1's Docker + Docker Compose setup. Coordinator Agent's container image is built from its own `Dockerfile` (produced in Code Generation), based on the same `mcr.microsoft.com/dotnet:10.0-aspnet` runtime image as Unit 1, and added as a second service block in the shared `docker-compose.yml`.

---

## 5. Observability Infrastructure

**Decision**: Reuse Unit 1's approach — structured JSON logs to stdout, captured by Docker daemon, inspected via `docker compose logs coordinator-agent`. No new logging infrastructure.

**Unit-2-specific log fields**: `query` (truncated), `intent`, `tool_name`, `status` (per NFR design logical-components.md).

---

## 6. Security Infrastructure

**Decision**: No change from Unit 1. No secrets, no auth, in Phase 1. Coordinator Agent makes only outbound calls to the MCP Server within the same Docker bridge network (no external egress required beyond what Unit 1 already allows for OS/image updates).

---

## 7. High Availability & Disaster Recovery

**Decision**: No change from Unit 1 — single-instance, single-AZ, no redundancy. Acceptable for Phase 1 POC.

---

## 8. Cost Estimation

**Delta over Unit 1's estimate**: Effectively $0 incremental — Coordinator Agent runs on the same already-provisioned EC2 instance and EBS volume. Total remains **~$20/month** as estimated in Unit 1's infrastructure design (the 10 GB EBS breakdown already reserved ~4 GB for "two containerized services").

---

## Summary of Infrastructure Decisions

| Component | Choice | Rationale |
|---|---|---|
| **Compute** | Shared EC2 (Unit 1 instance) | Stateless proxy service, no added workload |
| **Storage** | None | Stateless by NFR design |
| **Networking** | Reuse security group; Compose service DNS (`lightning-mcp-server:8000`) | Existing port 8001 rule covers Coordinator; no manual DNS |
| **Startup Ordering** | `depends_on` + `condition: service_healthy` | Avoids premature MCP calls; still fail-fast/no-retry at runtime |
| **Container Runtime** | Shared Docker Compose project, same base image | Consistency with Unit 1 |
| **Observability** | Shared stdout JSON logging | Manual inspection sufficient for POC |
| **Security** | None (Phase 1) | No secrets/auth in scope |
| **HA/DR** | None (single instance) | Acceptable for POC |
| **Cost** | +$0 incremental | Already provisioned for two containers |
| **Deployment Target Flexibility** | Environment-agnostic Docker Compose | Same compose project runs unchanged locally (Docker Desktop/Engine) or on EC2 — see `deployment-architecture.md` |

---

**Next**: See `deployment-architecture.md` for the updated Docker Compose service definition and deployment steps.
