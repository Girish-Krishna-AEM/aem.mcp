# NFR Design Patterns: Unit 3 — Infrastructure Orchestration

## Overview

Unit 3 has no business logic; its NFR design covers only orchestration-level concerns not already owned by Unit 1 or Unit 2's own NFR/infrastructure designs.

---

## 1. Resilience Pattern: Container Restart Policy

**Pattern**: `restart: unless-stopped` on both services.

**Rationale**: Recovers from crashes automatically; respects explicit `docker compose down`/`stop`. Each service's own fail-fast/no-retry behavior (Unit 1: stateless request handling; Unit 2: no-retry MCP client) is unaffected — this restart policy governs the container process, not in-request retry logic.

---

## 2. Scalability Pattern: Single-Replica, Single-Instance

**Pattern**: No scaling at the orchestration layer in Phase 1 — one instance of each service on one EC2 instance (or one local machine).

**Rationale**: Matches Units 1 & 2's stateless/single-instance NFR decisions. Phase 2 scaling (replicas, load balancer) is deferred per the Phase 1 scope.

---

## 3. Security Pattern: Environment Variable / Secrets Handling

**Pattern**: `.env.example` (placeholders, committed to source control) → actual `.env` (gitignored, created per-deployment, consumed by `docker-compose.yml` via `env_file` or shell export).

**Rationale**: No secrets exist in Phase 1 (no API keys, no credentials), but this establishes the pattern now so Phase 2 (real API keys, database URLs) doesn't require restructuring. Keeps `docker-compose.yml` itself free of environment-specific values, preserving the local/EC2 portability already established in Units 1 & 2's infrastructure designs.

---

## 4. Logical Component Pattern: Single Consolidated Compose File

**Pattern**: One `docker-compose.yml` at the repository root, combining both services. No environment-specific override files.

**Rationale**: The design is already environment-agnostic (no EC2-specific values baked into the compose file or Dockerfiles — see Unit 1 & 2 infra designs), so a single file serves both local and EC2 deployment without duplication.

---

## 5. Logical Component Pattern: Two-Layer Health Checks

**Pattern**:
- **Container-level**: `HEALTHCHECK` directive in each service's own Dockerfile (already present — Unit 1 and Unit 2 Dockerfiles) for `docker ps`/`docker compose ps` liveness visibility.
- **Compose-level**: `healthcheck:` stanza on `lightning-mcp-server` in `docker-compose.yml`, consumed by `coordinator-agent`'s `depends_on: condition: service_healthy` (already drafted in Unit 2's `deployment-architecture.md`) to sequence startup.

**Rationale**: Avoids duplicating healthcheck logic — the Compose-level check only exists where a startup dependency needs to observe it.

---

## Summary

| Category | Decision |
|---|---|
| Resilience | `restart: unless-stopped`, no orchestration-level retry |
| Scalability | Single-replica, single-instance (Phase 1) |
| Performance | N/A — owned by each service |
| Security | `.env.example` (committed) → `.env` (gitignored) |
| Logical Components | Single consolidated `docker-compose.yml`; two-layer healthchecks (Dockerfile + Compose) |
