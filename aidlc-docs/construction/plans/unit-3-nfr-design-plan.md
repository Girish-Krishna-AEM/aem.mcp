# NFR Design Plan: Unit 3 — Infrastructure Orchestration

## Context

Unit 3 has no business logic of its own — per `unit-of-work.md`, it owns NFR-2 (Containerization & Deployment) and the orchestration part of NFR-3 (health-check configuration) for Units 1 and 2. Its artifacts are `docker-compose.yml`, `.env.example`, an EC2 deployment guide, `DEPLOYMENT.md`, and a local-compose test procedure.

Because Units 1 and 2's own infrastructure designs (`aidlc-docs/construction/unit-1/infrastructure-design/`, `unit-2/infrastructure-design/`) already fully specified the shared EC2/Docker Compose topology, service healthchecks, startup ordering (`depends_on`/`service_healthy`), and the local/EC2 deployment-flexibility requirement, most NFR categories below resolve to "already decided — carry forward," not new design work.

## Plan Steps

- [ ] Step 1: Confirm resilience/scalability/performance/security patterns are inherited from Units 1 & 2 infra designs (no new orchestration-level patterns needed for Phase 1 POC).
- [ ] Step 2: Resolve the remaining orchestration-specific questions below (env var/secrets handling, compose file consolidation, restart policy consistency).
- [ ] Step 3: Generate `aidlc-docs/construction/unit-3/nfr-design/nfr-design-patterns.md`.
- [ ] Step 4: Generate `aidlc-docs/construction/unit-3/nfr-design/logical-components.md`.
- [ ] Step 5: Present completion message and wait for approval.

## Context-Appropriate Questions

### Resilience Patterns
Fail-fast error handling is owned by each service (Unit 1/2 NFR designs). Orchestration-level resilience is limited to container restart policy.
[Answer]: `restart: unless-stopped` for both services (already specified in Unit 1's deployment-architecture.md) — confirmed, no change.

### Scalability Patterns
Single-instance, single-replica Docker Compose deployment per Units 1 & 2 infra designs (no auto-scaling in Phase 1).
[Answer]: Confirmed — no orchestration-level scaling in Phase 1.

### Performance Patterns
No orchestration-level performance targets beyond each service's own NFRs (sub-second responses, per Unit 1 NFR design).
[Answer]: N/A — not an orchestration concern.

### Security Patterns
1. **Environment variable / secrets handling**: Should `.env.example` contain placeholder values only (committed to source control), with an actual `.env` file (gitignored) created per-deployment, or should env vars be passed directly via `docker-compose.yml`/shell export?
[Answer]: `.env.example` with placeholders, committed; actual `.env` file gitignored and created locally/on EC2 per deployment — standard Compose pattern, keeps secrets (none exist in Phase 1, but sets the pattern for Phase 2) out of source control.

2. Confirm no new network exposure beyond the ports already defined in Unit 1/2 infra designs (8000, 8001, 22).
[Answer]: Confirmed — no new ports.

### Logical Components
1. **Compose file consolidation**: Should the root `docker-compose.yml` be a single file combining both services (as drafted across Unit 1/2 deployment-architecture.md), or split into `docker-compose.yml` + override files per environment (local/EC2)?
[Answer]: Single `docker-compose.yml` at repo root for Phase 1 (simplicity); no environment-specific override files needed since the design is already environment-agnostic (local and EC2 use the identical file).

2. **Healthcheck directive placement**: Confirm both services' `HEALTHCHECK` directives live in their respective Dockerfiles (already present in Unit 1 and Unit 2 Dockerfiles) and Compose's `healthcheck:` stanza is only needed where `depends_on: condition: service_healthy` requires the Compose-level check (i.e., on `lightning-mcp-server`, consumed by `coordinator-agent`'s `depends_on`).
[Answer]: Confirmed — Dockerfile `HEALTHCHECK` for container-level liveness; Compose-level `healthcheck:` added to `lightning-mcp-server` service specifically to satisfy `coordinator-agent`'s `depends_on: condition: service_healthy` (already drafted in Unit 2's deployment-architecture.md).
