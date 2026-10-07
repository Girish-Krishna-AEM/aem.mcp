# EC2 Deployment Guide

This guide covers deploying the full stack (Lightning MCP Server + Coordinator Agent) to a single AWS EC2 instance. It references rather than duplicates the detailed, already-approved infrastructure design in `aidlc-docs/construction/unit-1/infrastructure-design/deployment-architecture.md` — see that document for the full rationale and alternative AWS Console steps.

## Prerequisites

- AWS account with EC2 permissions
- SSH key pair created in `us-east-1`
- Your IP address (for the SSH security group rule)

## 1. Launch EC2 Instance

Instance type `t3.small`, Amazon Linux 2023, `us-east-1a`, 10 GB `gp3` EBS volume. Full `aws ec2 run-instances` command and AWS Console steps: see Unit 1's `deployment-architecture.md`, Step 1.

## 2. Configure Security Group

Inbound: SSH (22, your IP), HTTP 8000 (MCP Server), HTTP 8001 (Coordinator Agent). Outbound: all. Full details: Unit 1's `deployment-architecture.md`, Step 2.

## 3. Allocate and Associate an Elastic IP

See Unit 1's `deployment-architecture.md`, Step 3.

## 4. SSH In and Install Docker + Docker Compose

See Unit 1's `deployment-architecture.md`, Steps 4–5, for the full `yum install docker` / `docker-compose-plugin` script.

## 5. Deploy the Stack (Unit 3-specific)

```bash
# On the EC2 instance
cd /opt/lightning-mcp

# Copy or clone the full repository (both unit-1/ and unit-2/ source, plus
# docker-compose.yml, .env.example at the repo root)
git clone <your-repo-url> .
# — or — copy the files via scp if not using git

# Create the environment file from the template
cp .env.example .env
# Edit .env if any values need to differ from the defaults (rare for Phase 1)

# Build and start both services
docker compose build
docker compose up -d

# Verify
docker compose ps
curl http://localhost:8000/health
curl http://localhost:8001/health
```

## 6. Test Connectivity From Your Local Machine

```bash
curl http://<elastic-ip>:8000/health
curl http://<elastic-ip>:8001/health
curl -X POST http://<elastic-ip>:8001/query \
  -H "Content-Type: application/json" \
  -d '{"query": "Is there lightning near Austin, TX?"}'
```

## 7. Manage the Deployment

```bash
docker compose logs -f
docker compose stop
docker compose restart
docker compose down
```

## Updating a Deployment

```bash
cd /opt/lightning-mcp
git pull origin main
docker compose build
docker compose up -d
```

## Cost & Disaster Recovery

Unchanged from Unit 1's infra design: ~$20/month total, manual EBS snapshot for backup. See `aidlc-docs/construction/unit-1/infrastructure-design/deployment-architecture.md` for details.

---

**See also**: `DEPLOYMENT.md` at the repository root for the consolidated local + EC2 walkthrough, including the local-first validation flow recommended before deploying here.
