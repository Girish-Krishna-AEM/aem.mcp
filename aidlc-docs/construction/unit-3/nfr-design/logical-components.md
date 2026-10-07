# Logical Components: Unit 3 — Infrastructure Orchestration

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│  docker-compose.yml (repo root)                 │
│  ┌─────────────────┐   ┌─────────────────────┐  │
│  │ lightning-mcp-   │   │ coordinator-agent   │  │
│  │ server (Unit 1)  │◄──┤ (Unit 2)            │  │
│  │ :8000, healthcheck│  │ :8001, depends_on:  │  │
│  │                  │   │   service_healthy   │  │
│  └─────────────────┘   └─────────────────────┘  │
├─────────────────────────────────────────────────┤
│  .env.example / .env (gitignored)                │
├─────────────────────────────────────────────────┤
│  Deployment Documentation                        │
│  (EC2 Deployment Guide, DEPLOYMENT.md)           │
└─────────────────────────────────────────────────┘
```

---

## Component 1: docker-compose.yml

**Responsibility**: Single source of truth for service definitions, networking, health-check wiring, and startup ordering for both units.

**Contents** (consolidating what Unit 1 and Unit 2's infra designs already drafted):
- `lightning-mcp-server` service: build context `./unit-1/src/LightningMcpServer`, port 8000, `healthcheck:` stanza, `restart: unless-stopped`
- `coordinator-agent` service: build context `./unit-2/src/CoordinatorAgent`, port 8001, `depends_on: lightning-mcp-server: condition: service_healthy`, `restart: unless-stopped`
- Implicit default bridge network (Compose-managed service-name DNS)
- `env_file: .env` (or equivalent) for both services

---

## Component 2: .env.example / .env

**Responsibility**: Template and actual environment variable files.

**`.env.example`** (committed):
```
LISTEN_PORT_MCP=8000
LISTEN_PORT_COORDINATOR=8001
LOG_LEVEL=INFO
MCP_SERVER_URL=http://lightning-mcp-server:8000
```

**`.env`** (gitignored, created per-deployment): identical structure, actual values — identical between local and EC2 in Phase 1 since no secrets exist.

---

## Component 3: Deployment Documentation

**EC2 Deployment Guide**: Instance provisioning steps (already detailed in Unit 1's `infrastructure-design/deployment-architecture.md`) — referenced, not duplicated.

**DEPLOYMENT.md**: Consolidated, unit-agnostic deployment guide covering both local and EC2 targets in one document (build, run, verify, troubleshoot, rollback) — see Unit 3 Code Generation for the actual file.

---

## Request Flow (Startup Sequence)

```
1. docker compose up -d --build
2. Compose builds lightning-mcp-server and coordinator-agent images
3. lightning-mcp-server container starts
   → Compose healthcheck polls GET /health every 10s
4. Once healthy, coordinator-agent container starts
   → Resolves MCP_SERVER_URL via Compose DNS (lightning-mcp-server:8000)
5. Both containers running; verify via:
   curl http://<host>:8000/health
   curl http://<host>:8001/health
```

---

## Component Dependencies

| Component | Depends On |
|---|---|
| `coordinator-agent` (Compose service) | `lightning-mcp-server` (service_healthy) |
| `lightning-mcp-server` (Compose service) | None |
| `.env` | `.env.example` (template) |
| `DEPLOYMENT.md` | Unit 1 & Unit 2 infrastructure-design docs (referenced, not duplicated) |

---

## Phase 2 Additions

- Split `docker-compose.yml` into base + environment-specific override files if local/EC2 configs diverge (e.g., resource limits, log aggregation sidecars).
- Add Terraform/CloudFormation for EC2 provisioning (currently manual).
- Add `docker-compose.prod.yml` with resource limits and a log-aggregation sidecar once Observability layer is in scope.

---

**Last Updated**: 2026-10-04
