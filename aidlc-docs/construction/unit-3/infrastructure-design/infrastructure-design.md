# Infrastructure Design: Unit 3 — Infrastructure Orchestration

## Overview

Unit 3 introduces no new infrastructure. It consolidates the decisions already made and approved in Unit 1's and Unit 2's infrastructure designs into the actual orchestration artifacts that Code Generation will produce.

---

## 1. Compute, Storage, Networking, Observability, Security: Fully Inherited

All decisions below are unchanged from `aidlc-docs/construction/unit-1/infrastructure-design/infrastructure-design.md` (and reaffirmed by Unit 2):

| Component | Decision (inherited) |
|---|---|
| **Compute** | Single EC2 `t3.small`, us-east-1, Amazon Linux 2023 |
| **Storage** | 10 GB EBS gp3; no persistent databases |
| **Networking** | One security group (SSH 22, HTTP 8000/8001); Docker Compose bridge network with service-name DNS |
| **Observability** | stdout JSON logs via `docker compose logs`; manual `/health` checks |
| **Security** | No secrets/auth in Phase 1 |
| **HA/DR** | Single-instance, single-AZ; manual EBS snapshot (optional) |
| **Cost** | ~$20/month total (unchanged — both services share the one instance) |

---

## 2. Unit-3-Specific Decision: EC2 Provisioning Remains Manual (Phase 1)

**Decision**: Unit 3's "EC2 Deployment Guide" documents the manual EC2 launch + Docker Compose deployment steps already specified in Unit 1's infra design. No Terraform/CloudFormation introduced in Phase 1.

**Rationale**: Keeps Phase 1 POC scope minimal, consistent with the approved Phased Roadmap (IaC explicitly deferred to Phase 2 in Unit 1's infra design). Introducing IaC tooling now would be scope creep beyond what any stage has asked for.

---

## 3. Unit 3's Role: Consolidation, Not New Design

Unit 3's Code Generation stage will produce:
1. **`docker-compose.yml`** (repo root) — combines the `lightning-mcp-server` and `coordinator-agent` service blocks already drafted in Unit 1 & 2's `deployment-architecture.md` files into one file.
2. **`.env.example`** — template for `LISTEN_PORT_MCP`, `LISTEN_PORT_COORDINATOR`, `LOG_LEVEL`, `MCP_SERVER_URL` (per Unit 3 NFR design's logical-components.md).
3. **EC2 Deployment Guide** — references Unit 1's existing step-by-step EC2 provisioning instructions rather than duplicating them.
4. **`DEPLOYMENT.md`** — single consolidated guide covering both local and EC2 targets (per the local-run flexibility requirement established during Unit 2's Infrastructure Design).
5. **Local compose test procedure** — verification steps for running the full stack locally before EC2 deployment.

---

## Summary of Infrastructure Decisions

| Component | Choice | Rationale |
|---|---|---|
| **All infra (compute/storage/network/observability/security)** | Inherited unchanged from Unit 1 & 2 | No new infrastructure needed for pure orchestration |
| **Provisioning approach** | Manual (Phase 1), IaC deferred to Phase 2 | Matches approved roadmap, avoids scope creep |
| **Deployment flexibility** | Single compose file works locally and on EC2 unchanged | Explicit user requirement from Unit 2 approval |

---

**Next**: See `deployment-architecture.md` for the consolidated topology view and the artifact list Code Generation will produce.
