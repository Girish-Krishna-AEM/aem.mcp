# Code Generation Plan: Unit 3 — Infrastructure Orchestration

## Unit Context

**Unit**: Infrastructure Orchestration
**Type**: Greenfield deployment/orchestration artifacts (no application code)
**Stories**: 5 (docker-compose.yml, .env.example, EC2 Deployment Guide, DEPLOYMENT.md, Test Local docker-compose)

### Unit Responsibilities

- Consolidate Unit 1 & Unit 2's service definitions into one `docker-compose.yml`
- Provide an environment variable template (`.env.example`)
- Document EC2 deployment (referencing, not duplicating, Unit 1's existing provisioning steps)
- Document the full deployment procedure for both local and EC2 targets in `DEPLOYMENT.md`
- Verify the stack actually runs locally via Docker Compose

### Stories Implemented by This Unit

1. **Story 1: Create docker-compose.yml** — Orchestrate both services, networking, health-checks, startup ordering
2. **Story 2: Create .env.example** — Environment variable template
3. **Story 3: Create EC2 Deployment Guide** — Instance type, AMI, security group, startup (references Unit 1's infra design)
4. **Story 4: Create DEPLOYMENT.md** — Comprehensive deployment documentation (local + EC2)
5. **Story 5: Test Local docker-compose** — Build images, run stack, verify health-checks, submit queries, verify logs

### Dependencies

- **Unit 1**: `unit-1/src/LightningMcpServer/Dockerfile` (already generated)
- **Unit 2**: `unit-2/src/CoordinatorAgent/Dockerfile` (already generated)
- **Design source**: `aidlc-docs/construction/unit-3/nfr-design/` and `infrastructure-design/` (already approved)

---

## Code Generation Steps

### Step 1: Create docker-compose.yml
- [x] Create `docker-compose.yml` at repo root
- [x] `lightning-mcp-server` service: build context `./unit-1`, dockerfile `src/LightningMcpServer/Dockerfile`, port 8000, `healthcheck:`, `restart: unless-stopped`, `env_file: .env`
- [x] `coordinator-agent` service: build context `./unit-2`, dockerfile `src/CoordinatorAgent/Dockerfile`, port 8001, `depends_on: lightning-mcp-server: condition: service_healthy`, `restart: unless-stopped`, `env_file: .env`
- [x] No explicit `networks:` block (default bridge network is sufficient)

**Story Coverage**: Story 1

---

### Step 2: Create .env.example
- [x] Create `.env.example` at repo root with placeholder values for `LISTEN_PORT`, `LOG_LEVEL`, `MCP_SERVER_URL`, `ASPNETCORE_URLS` (both services' variables, prefixed where needed to avoid collision)
- [x] Add `.env` to `.gitignore` (create `.gitignore` entry if not already present)

**Story Coverage**: Story 2

---

### Step 3: Create EC2 Deployment Guide
- [x] Create `unit-3/docs/ec2-deployment-guide.md`
- [x] Reference Unit 1's existing step-by-step EC2 provisioning (launch instance, security group, Elastic IP, install Docker) rather than duplicating
- [x] Add Unit-3-specific step: clone/copy repo to `/opt/lightning-mcp`, copy `.env.example` → `.env`, `docker compose up -d --build`

**Story Coverage**: Story 3

---

### Step 4: Create DEPLOYMENT.md
- [x] Create `DEPLOYMENT.md` at repo root
- [x] Sections: Prerequisites, Local Deployment (full walkthrough), EC2 Deployment (links to `ec2-deployment-guide.md`), Verification, Troubleshooting, Rollback
- [x] Emphasize identical commands for both targets (only `<host>` changes)

**Story Coverage**: Story 4

---

### Step 5: Test Local docker-compose
- [x] Validate `docker compose config` (compose file syntax/schema) — passed
- [ ] Run `docker compose build` — **not run**: Docker Desktop engine unavailable in this execution environment (documented, not skipped silently)
- [ ] Run `docker compose up -d` — not run (same reason)
- [ ] Verify `docker compose ps` shows both containers healthy — not run
- [ ] Verify `GET /health` on both services — not run
- [ ] Verify `POST /query` end-to-end (Coordinator → MCP Server) with a representative query — not run
- [ ] Verify `docker compose logs` shows structured JSON from both services — not run
- [x] Record results and environment-limitation note in `aidlc-docs/construction/unit-3/code/local-compose-test-results.md`, including exact commands for the user to complete verification on a machine with Docker running

**Story Coverage**: Story 5

---

## Summary

**Total Steps**: 5
**Directory Structure**:
```
<repo-root>/
├── docker-compose.yml
├── .env.example
├── DEPLOYMENT.md
└── unit-3/
    └── docs/
        └── ec2-deployment-guide.md
```

**Approval Gate**: User must review and approve this plan before proceeding to Part 2 (Code Generation).

---

**Next Step**: Please review the above code generation plan and approve or request changes.
