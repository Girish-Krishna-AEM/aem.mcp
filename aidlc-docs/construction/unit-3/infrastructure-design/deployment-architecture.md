# Deployment Architecture: Unit 3 — Infrastructure Orchestration

## Consolidated Topology (Final — Both Units)

```
┌─────────────────────────────────────────────────────────┐
│  Either: Local machine (Docker Desktop/Engine)           │
│       Or: AWS EC2 t3.small, us-east-1a (Elastic IP)      │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  docker compose up -d --build                   │   │
│  │                                                 │   │
│  │  ┌────────────────────────────────────┐         │   │
│  │  │  lightning-mcp-server               │         │   │
│  │  │  :8000, healthcheck, restart policy │         │   │
│  │  └────────────────────────────────────┘         │   │
│  │              ▲ depends_on: service_healthy       │   │
│  │  ┌────────────────────────────────────┐         │   │
│  │  │  coordinator-agent                  │         │   │
│  │  │  :8001, MCP_SERVER_URL=...8000      │         │   │
│  │  └────────────────────────────────────┘         │   │
│  │                                                 │   │
│  │  env_file: .env  (from .env.example template)   │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│  Access: http://localhost:8001  OR  http://<ec2-ip>:8001│
└─────────────────────────────────────────────────────────┘
```

---

## Artifacts Produced by Unit 3's Code Generation

| Artifact | Location | Purpose |
|---|---|---|
| `docker-compose.yml` | Repo root | Combines both service definitions; single file for local and EC2 |
| `.env.example` | Repo root | Template for env vars; copy to `.env` (gitignored) per deployment |
| EC2 Deployment Guide | `unit-3/docs/ec2-deployment-guide.md` | References Unit 1's provisioning steps; EC2-specific instructions only |
| `DEPLOYMENT.md` | Repo root | Consolidated local + EC2 deployment walkthrough, verification, rollback |
| Local compose test notes | Included in `DEPLOYMENT.md` | Build, run, verify, teardown steps for local-first validation |

---

## Verification Flow (Both Targets)

```bash
docker compose build
docker compose up -d
docker compose ps
curl http://<host>:8000/health
curl http://<host>:8001/health
curl -X POST http://<host>:8001/query -H "Content-Type: application/json" -d '{"query":"Is there lightning near Austin, TX?"}'
docker compose logs -f
docker compose down
```

`<host>` is `localhost` for local runs or the EC2 Elastic IP for cloud deployment — identical commands otherwise, per the local/EC2 parity requirement.

---

**Last Updated**: 2026-10-04
