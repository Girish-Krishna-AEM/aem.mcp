# Deployment Architecture: Unit 2 — Coordinator Agent

## Updated Network Topology (Both Units)

```
┌─────────────────────────────────────────────────────────┐
│  AWS VPC (default, us-east-1a)                          │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  EC2 Instance (t3.small, 10 GB) — shared         │   │
│  │                                                 │   │
│  │  ┌─────────────────────────────────────────┐   │   │
│  │  │  Docker Daemon                          │   │   │
│  │  │  ┌────────────────────────────────────┐ │   │   │
│  │  │  │  Container: lightning-mcp-server   │ │   │   │
│  │  │  │  Port 8000 (HTTP)                  │ │   │   │
│  │  │  │  healthcheck: GET /health           │ │   │   │
│  │  │  └────────────────────────────────────┘ │   │   │
│  │  │              ▲                          │   │   │
│  │  │   depends_on │ condition: service_healthy│   │   │
│  │  │              │                          │   │   │
│  │  │  ┌────────────────────────────────────┐ │   │   │
│  │  │  │  Container: coordinator-agent      │ │   │   │
│  │  │  │  Port 8001 (HTTP)                  │ │   │   │
│  │  │  │  env: MCP_SERVER_URL=              │ │   │   │
│  │  │  │    http://lightning-mcp-server:8000│ │   │   │
│  │  │  └────────────────────────────────────┘ │   │   │
│  │  │                                         │   │   │
│  │  │  Docker Network (bridge, Compose DNS)   │   │   │
│  │  └────────────────────────────────────────┘   │   │
│  │                                                 │   │
│  │  Elastic IP: <public-ip>                       │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│  Security Group: SSH (22), HTTP (8000, 8001) — unchanged│
│                                                         │
└─────────────────────────────────────────────────────────┘
        │
        │ Internet
        ▼
    User/Tester
    (connects via public IP:8001 to Coordinator Agent)
```

---

## Docker Compose Service Addition

Add the following service block to the shared `docker-compose.yml` (alongside the existing `lightning-mcp-server` service from Unit 1):

```yaml
services:
  lightning-mcp-server:
    # ... existing Unit 1 definition unchanged ...
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 10s

  coordinator-agent:
    build:
      context: ./unit-2/src/CoordinatorAgent
      dockerfile: Dockerfile
    container_name: coordinator-agent
    ports:
      - "8001:8001"
    environment:
      - LISTEN_PORT=8001
      - LOG_LEVEL=INFO
      - MCP_SERVER_URL=http://lightning-mcp-server:8000
      - ASPNETCORE_URLS=http://0.0.0.0:8001
    depends_on:
      lightning-mcp-server:
        condition: service_healthy
    networks:
      - default
```

**Note**: `networks: default` is implicit — both services join the Compose-created bridge network automatically; no explicit `networks:` top-level block is required unless a custom network name is desired.

---

## Deployment Steps (Additive to Unit 1)

1. Ensure Unit 1's EC2 instance, Docker, and Docker Compose are already provisioned (see Unit 1 `infrastructure-design.md`).
2. Pull/copy Unit 2 source (`unit-2/src/CoordinatorAgent`) to `/opt/lightning-mcp/unit-2` on the EC2 instance (sibling to Unit 1's `unit-1` directory).
3. Add the `coordinator-agent` service block above to the existing `docker-compose.yml`.
4. Add a `healthcheck:` block to the existing `lightning-mcp-server` service if not already present (required for `depends_on: condition: service_healthy` to function).
5. Run `docker compose up -d --build` to build and start both containers.
6. Verify:
   ```bash
   docker compose ps
   curl http://<elastic-ip>:8001/health
   curl -X POST http://<elastic-ip>:8001/query -H "Content-Type: application/json" -d '{"query":"Is there lightning near Austin, TX?"}'
   ```

---

## Local Development Deployment

Same principle as Unit 1: the `coordinator-agent` service block above is environment-agnostic — no EC2-specific values. Running the full stack locally is identical to Unit 1's local workflow, just with the `coordinator-agent` service included:

```bash
docker compose up -d --build
curl http://localhost:8001/health
curl -X POST http://localhost:8001/query -H "Content-Type: application/json" -d '{"query":"Is there lightning near Austin, TX?"}'
```

`MCP_SERVER_URL=http://lightning-mcp-server:8000` resolves via Docker Compose's internal DNS the same way locally and on EC2 — no code or config changes needed between targets.

---

## Rollback

`docker compose stop coordinator-agent && docker compose rm -f coordinator-agent` — Unit 1's MCP Server is unaffected since it has no dependency on Unit 2.

---

**Last Updated**: 2026-10-04
