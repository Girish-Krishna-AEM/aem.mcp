# Deployment Guide

Deploys the Lightning Detection MCP POC (Lightning MCP Server + Coordinator Agent) via Docker Compose. The same `docker-compose.yml` and Dockerfiles run **unchanged** locally or on EC2 — only the host address you connect to differs.

## Prerequisites

- Docker Desktop (Windows/Mac) or Docker Engine + Compose plugin (Linux)
- For EC2 deployment: see `unit-3/docs/ec2-deployment-guide.md` for instance provisioning first

## 1. Configure Environment

```bash
cp .env.example .env
# Defaults work as-is for Phase 1 (no secrets); edit only if you need non-default ports/log level.
```

## 2. Local Deployment (Recommended First Step)

Validate the full stack locally before deploying to EC2:

```bash
docker compose build
docker compose up -d

docker compose ps
curl http://localhost:8000/health
curl http://localhost:8001/health

curl -X POST http://localhost:8001/query \
  -H "Content-Type: application/json" \
  -d '{"query": "Is there lightning near Austin, TX?"}'

docker compose logs -f
```

Tear down:

```bash
docker compose down
```

## 3. EC2 Deployment

Once validated locally, follow `unit-3/docs/ec2-deployment-guide.md` to provision an EC2 instance and run the identical `docker compose build && docker compose up -d` there. Replace `localhost` with the instance's Elastic IP in all verification commands.

## 4. Verification Checklist

| Check | Command | Expected |
|---|---|---|
| Both containers running | `docker compose ps` | `lightning-mcp-server` and `coordinator-agent` both `Up (healthy)` |
| MCP Server health | `curl http://<host>:8000/health` | `{"status":"healthy",...}` |
| Coordinator health | `curl http://<host>:8001/health` | `{"status":"healthy",...,"mcp_server":"reachable"}` |
| End-to-end query | `POST /query` with a known-city query | 200 with `toolName`, `result`, `summary` |
| Structured logs | `docker compose logs` | JSON lines from both services |

## 5. Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| `coordinator-agent` never starts | `lightning-mcp-server` healthcheck failing | `docker compose logs lightning-mcp-server`; verify port 8000 isn't in use |
| `/query` returns 503 | MCP Server unreachable from Coordinator | Confirm `MCP_SERVER_URL` in `.env` matches the Compose service name (`lightning-mcp-server`) |
| `/query` returns 400 "Unable to determine..." | Query didn't match any intent keyword | Rephrase using "lightning", "forecast", "sensor", or "status"/"device" — see `aidlc-docs/construction/unit-2/code/intent-and-parameter-reference.md` |
| Port already in use | Another process bound to 8000/8001 | Stop the conflicting process, or change `LISTEN_PORT_*` in `.env` and the `ports:` mapping in `docker-compose.yml` |

## 6. Rollback

```bash
docker compose down
git checkout <previous-commit-or-tag>
docker compose up -d --build
```

For EC2-only rollback of a single service: `docker compose stop <service> && docker compose rm -f <service>`, then redeploy that service's image.

## Related Documentation

- `aidlc-docs/construction/unit-1/infrastructure-design/` — Unit 1's full AWS topology, cost, and provisioning rationale
- `aidlc-docs/construction/unit-2/infrastructure-design/` — Unit 2's infra decisions and local/EC2 parity requirement
- `aidlc-docs/construction/unit-3/infrastructure-design/` — consolidated orchestration design
- `unit-1/README.md`, `unit-2/README.md` — per-service build/run/API details
