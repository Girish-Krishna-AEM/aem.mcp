# Deployment Architecture: Unit 1 — Lightning MCP Server

## Deployment Overview

This document provides the detailed deployment architecture for Unit 1 (Lightning MCP Server) on AWS EC2, including step-by-step provisioning instructions, service topology, and operational procedures.

---

## Phase 1 Deployment Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         AWS us-east-1                               │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                  VPC (default, us-east-1a)                  │   │
│  │                                                             │   │
│  │  ┌───────────────────────────────────────────────────────┐ │   │
│  │  │   EC2 Instance: t3.small                             │ │   │
│  │  │   - Public IP: <Elastic-IP>                          │ │   │
│  │  │   - Private IP: 10.0.x.x (auto-assigned)             │ │   │
│  │  │   - OS: Amazon Linux 2023                            │ │   │
│  │  │   - Storage: 10 GB EBS gp3                           │ │   │
│  │  │                                                       │ │   │
│  │  │   ┌─────────────────────────────────────────────────┐ │ │   │
│  │  │   │   Docker Daemon (running)                       │ │ │   │
│  │  │   │                                                 │ │ │   │
│  │  │   │   ┌──────────────────────────────────────────┐ │ │ │   │
│  │  │   │   │ Service 1: lightning-mcp-server          │ │ │ │   │
│  │  │   │   │ Container Port: 8000 (HTTP/SSE)          │ │ │ │   │
│  │  │   │   │ Environment:                             │ │ │ │   │
│  │  │   │   │   LISTEN_PORT=8000                       │ │ │ │   │
│  │  │   │   │   LOG_LEVEL=INFO                         │ │ │ │   │
│  │  │   │   │ Health: GET /health → 200 OK             │ │ │ │   │
│  │  │   │   └──────────────────────────────────────────┘ │ │ │   │
│  │  │   │                                                 │ │ │   │
│  │  │   │   ┌──────────────────────────────────────────┐ │ │ │   │
│  │  │   │   │ Service 2: coordinator-agent             │ │ │ │   │
│  │  │   │   │ Container Port: 8001 (HTTP)              │ │ │ │   │
│  │  │   │   │ Environment:                             │ │ │ │   │
│  │  │   │   │   MCP_SERVER_URL=http://...              │ │ │ │   │
│  │  │   │   │   LISTEN_PORT=8001                       │ │ │ │   │
│  │  │   │   │   LOG_LEVEL=INFO                         │ │ │ │   │
│  │  │   │   │ Health: GET /health → 200 OK             │ │ │ │   │
│  │  │   │   └──────────────────────────────────────────┘ │ │ │   │
│  │  │   │                                                 │ │ │   │
│  │  │   │   Docker Network (bridge)                       │ │ │   │
│  │  │   │   - Container DNS: service hostnames           │ │ │   │
│  │  │   │   - lightning-mcp-server resolves internally   │ │ │   │
│  │  │   └─────────────────────────────────────────────────┘ │ │   │
│  │  │                                                       │ │   │
│  │  └───────────────────────────────────────────────────────┘ │   │
│  │                                                             │   │
│  │   Security Group (Attached to EC2)                         │   │
│  │   Inbound:                                                 │   │
│  │   - Port 22/TCP from <your-ip>/32 (SSH)                  │   │
│  │   - Port 8000/TCP from 0.0.0.0/0 (MCP Server)            │   │
│  │   - Port 8001/TCP from 0.0.0.0/0 (Coordinator Agent)     │   │
│  │   Outbound: All (0.0.0.0/0)                              │   │
│  │                                                             │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
        ▲
        │ Internet Traffic
        │
    User/Tester Browser or API Client
    (external access via <Elastic-IP>:8001)
```

---

## Service Topology

### Lightning MCP Server (Unit 1)

| Attribute | Value |
|-----------|-------|
| **Container Image** | Built from Dockerfile (Code Generation phase), base: `mcr.microsoft.com/dotnet:10.0-aspnet` |
| **Container Port** | 8000 (HTTP/SSE) |
| **Expose to Host** | 8000:8000 (Docker Compose maps container:host) |
| **Environment Variables** | `LISTEN_PORT=8000`, `LOG_LEVEL=INFO`, `MCP_PROTOCOL=http-sse` |
| **Startup Command** | `dotnet LightningMcpServer.dll` (set in Dockerfile) |
| **Health Check** | `GET /health` returns `{"status": "healthy", "timestamp": "..."}` HTTP 200 |
| **Logging** | Structured JSON to stdout (captured by Docker daemon) |
| **Dependencies** | None (standalone service; relies on LightningCommon shared library) |
| **Restart Policy** | `unless-stopped` (restart on crash, but stop on explicit `docker compose down`) |

### Coordinator Agent (Unit 2, included for completeness)

| Attribute | Value |
|-----------|-------|
| **Container Image** | Built from Dockerfile (Code Generation phase), base: `mcr.microsoft.com/dotnet:10.0-aspnet` |
| **Container Port** | 8001 (HTTP) |
| **Expose to Host** | 8001:8001 |
| **Environment Variables** | `MCP_SERVER_URL=http://lightning-mcp-server:8000`, `LISTEN_PORT=8001`, `LOG_LEVEL=INFO` |
| **Startup Command** | `dotnet CoordinatorAgent.dll` |
| **Health Check** | `GET /health` returns `{"status": "healthy", "timestamp": "..."}` HTTP 200 |
| **Logging** | Structured JSON to stdout |
| **Dependencies** | Depends on `lightning-mcp-server` service (Docker Compose `depends_on`) |
| **Restart Policy** | `unless-stopped` |

---

## Docker Compose File Structure (Overview)

```yaml
version: '3.9'

services:
  lightning-mcp-server:
    build:
      context: .
      dockerfile: ./src/LightningMcpServer/Dockerfile
    container_name: lightning-mcp-server
    ports:
      - "8000:8000"
    environment:
      - LISTEN_PORT=8000
      - LOG_LEVEL=INFO
      - MCP_PROTOCOL=http-sse
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  coordinator-agent:
    build:
      context: .
      dockerfile: ./src/CoordinatorAgent/Dockerfile
    container_name: coordinator-agent
    ports:
      - "8001:8001"
    environment:
      - MCP_SERVER_URL=http://lightning-mcp-server:8000
      - LISTEN_PORT=8001
      - LOG_LEVEL=INFO
    depends_on:
      lightning-mcp-server:
        condition: service_healthy
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8001/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
```

**Key Notes**:
- `depends_on` with `condition: service_healthy` ensures MCP Server is healthy before starting Coordinator Agent.
- `healthcheck` pings `/health` endpoint; Docker uses this for orchestration.
- `restart: unless-stopped` recovers from crashes; preserves manual stop commands.
- Environment variables inject configuration at runtime.

---

## Step-by-Step Deployment Instructions

### Prerequisites

- AWS account with EC2 permissions
- SSH key pair created in us-east-1
- Your IP address (for SSH security group rule)

### Step 1: Launch EC2 Instance

**AWS Console or AWS CLI**:

```bash
# Using AWS CLI (replace key-name, security-group, subnet as needed)
aws ec2 run-instances \
  --image-id ami-0c55b159cbfafe1f0 \
  (Amazon Linux 2023 AMI in us-east-1) \
  --instance-type t3.small \
  --region us-east-1 \
  --availability-zone us-east-1a \
  --key-name <your-key-pair-name> \
  --security-groups <security-group-name> \
  --block-device-mappings "DeviceName=/dev/xvda,Ebs={VolumeSize=10,VolumeType=gp3}" \
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=lightning-mcp-poc}]"
```

**AWS Console** (Alternative):
1. EC2 Dashboard → Instances → Launch Instance
2. Choose "Amazon Linux 2023 AMI"
3. Instance Type: `t3.small`
4. Network: Default VPC, us-east-1a
5. Storage: 10 GB gp3
6. Security Group: Create new with rules below
7. Key Pair: Select existing or create
8. Launch

### Step 2: Configure Security Group

**Inbound Rules**:
| Protocol | Port | Source | Description |
|----------|------|--------|---|
| TCP | 22 | <your-ip>/32 | SSH |
| TCP | 8000 | 0.0.0.0/0 | MCP Server |
| TCP | 8001 | 0.0.0.0/0 | Coordinator Agent |

**Outbound Rules**:
| Protocol | Port | Destination |
|----------|------|---|
| All | All | 0.0.0.0/0 |

### Step 3: Allocate and Associate Elastic IP

```bash
# Allocate Elastic IP
aws ec2 allocate-address --region us-east-1 --domain vpc

# Associate with EC2 instance (replace instance-id and allocation-id)
aws ec2 associate-address \
  --instance-id i-xxxxxx \
  --allocation-id eipalloc-xxxxx \
  --region us-east-1
```

**Note**: Or use AWS Console: EC2 → Network & Security → Elastic IPs → Allocate and Associate.

### Step 4: SSH into EC2 Instance

```bash
# SSH (replace key.pem and <elastic-ip>)
ssh -i /path/to/key.pem ec2-user@<elastic-ip>

# Verify connectivity
$ whoami
ec2-user
```

### Step 5: Install Docker and Docker Compose

Run this on the EC2 instance:

```bash
#!/bin/bash
set -e

# Update system
sudo yum update -y

# Install Docker
sudo yum install docker -y
sudo systemctl start docker
sudo systemctl enable docker

# Add ec2-user to docker group (allows running docker without sudo)
sudo usermod -aG docker ec2-user

# Install Docker Compose v2
sudo yum install docker-compose-plugin -y

# Verify installations
docker --version
docker compose version

# Create application directory
sudo mkdir -p /opt/lightning-mcp
sudo chown ec2-user:ec2-user /opt/lightning-mcp

echo "Docker and Docker Compose installed successfully"
```

### Step 6: Deploy Application

On the EC2 instance (in `/opt/lightning-mcp`):

```bash
# Clone/download source code (assuming GitHub repo)
cd /opt/lightning-mcp
git clone https://github.com/<your-repo>/lightning-mcp-poc.git .

# Or, copy docker-compose.yml and Dockerfiles manually

# Build and start containers
docker compose build
docker compose up -d

# Verify containers are running
docker compose ps

# Check service health
curl http://localhost:8000/health  # Should return {"status": "healthy"}
curl http://localhost:8001/health  # Should return {"status": "healthy"}

# View logs
docker compose logs -f
```

### Step 7: Test Connectivity

From your local machine:

```bash
# Test MCP Server health check
curl http://<elastic-ip>:8000/health

# Test Coordinator Agent health check
curl http://<elastic-ip>:8001/health

# Example: Query Coordinator Agent with natural-language intent
curl -X POST http://<elastic-ip>:8001/query \
  -H "Content-Type: application/json" \
  -d '{"query": "Is there lightning near Austin, TX?"}'
```

### Step 8: Manage Containers

```bash
# View logs (real-time)
docker compose logs -f

# Filter logs by service
docker compose logs lightning-mcp-server
docker compose logs coordinator-agent

# Stop services
docker compose stop

# Restart services
docker compose restart

# Stop and remove containers (but preserve images)
docker compose down

# Stop, remove containers, and rebuild images
docker compose down --rmi local
docker compose up -d
```

---

## Operational Procedures

### Daily Monitoring

**Health Checks**:
```bash
# Check both services are healthy
curl http://<elastic-ip>:8000/health
curl http://<elastic-ip>:8001/health

# View current logs
docker compose logs
```

**Disk Space**:
```bash
# Check EBS volume usage
df -h /
```

### Updating Services

```bash
# Pull latest source code
cd /opt/lightning-mcp
git pull origin main

# Rebuild containers
docker compose build

# Restart with new images
docker compose up -d
```

### Scaling (Phase 2)

**For Phase 2** (if workload increases):
- Upgrade EC2 instance type: `t3.small` → `t3.medium` (larger vCPU/RAM)
- Add load balancer (ALB) if multiple EC2 instances deployed
- Use ECS or EKS for container orchestration

---

## Disaster Recovery (Phase 1)

### EBS Snapshot

```bash
# Create manual snapshot of EBS volume for backup
aws ec2 create-snapshot \
  --volume-id vol-xxxxx \
  --description "Lightning MCP POC backup $(date)" \
  --region us-east-1
```

### Rebuild from Snapshot

If EC2 instance fails:
1. Create new EC2 instance (t3.small, same AZ)
2. Attach snapshot-restored EBS volume
3. SSH and redeploy via `docker compose up`

---

## Cost Breakdown (Monthly)

| Component | Cost |
|-----------|------|
| EC2 t3.small (730 hours @ $0.0252/hr) | $18.40 |
| EBS 10 GB gp3 storage | $1.00 |
| Data transfer (minimal) | <$1.00 |
| **Total** | **~$20/month** |

**Optimization Tips for Future Phases**:
- Use Reserved Instances for predictable workloads (20–40% discount)
- Use Spot Instances for non-critical services (~80% savings but interruptible)
- Enable AWS Compute Optimizer for right-sizing recommendations

---

## Local Development Deployment (Run Before Deploying to EC2)

The `docker-compose.yml` and both Dockerfiles are environment-agnostic by design — no AWS-specific values (Elastic IP, security group, instance type) are baked into the compose file or application config. The exact same compose project runs unchanged on a developer laptop (Docker Desktop / Docker Engine) and on the EC2 instance; only the external access address differs (`localhost` vs. `<elastic-ip>`).

### Prerequisites
- Docker Desktop (Windows/Mac) or Docker Engine + Compose plugin (Linux) installed locally
- .NET 10 SDK (optional — only needed if building/running outside containers)

### Run Locally

```bash
# From the repository root (same docker-compose.yml used for EC2)
docker compose build
docker compose up -d

# Verify
docker compose ps
curl http://localhost:8000/health   # Lightning MCP Server
curl http://localhost:8001/health   # Coordinator Agent

# Example end-to-end query
curl -X POST http://localhost:8001/query \
  -H "Content-Type: application/json" \
  -d '{"query": "Is there lightning near Austin, TX?"}'

# Logs
docker compose logs -f

# Tear down
docker compose down
```

### Local vs. EC2 — What Differs

| Aspect | Local | EC2 |
|---|---|---|
| **Compose file** | Identical (`docker-compose.yml`) | Identical |
| **Dockerfiles** | Identical | Identical |
| **External access** | `http://localhost:8000` / `:8001` | `http://<elastic-ip>:8000` / `:8001` |
| **Security group** | N/A (local firewall only) | AWS security group (Step 2 above) |
| **Health checks / depends_on** | Same behavior | Same behavior |
| **Persistence** | None (same as EC2 — stateless POC) | None |

**Guidance for Code Generation stage**: Keep all service URLs, ports, and timeouts configurable via environment variables (already specified in the NFR/infra designs) with sensible defaults — never hardcode the EC2 Elastic IP or AWS-specific hostnames into application code or Dockerfiles, so the same image/compose file is deployable to either target without modification.

---

## Next Steps (Code Generation)

With infrastructure designed, the Code Generation phase will:
1. Write Dockerfile for Lightning MCP Server
2. Write Dockerfile for Coordinator Agent
3. Generate docker-compose.yml
4. Produce deployment instructions (which leverage this architecture)

---

**Summary**: Phase 1 POC deploys a single t3.small EC2 instance in us-east-1 running two Docker containers (MCP Server on port 8000, Coordinator Agent on port 8001) with JSON structured logs and health checks. Minimal cost (~$20/month), suitable for proof-of-concept validation. Phase 2 scales infrastructure based on real workload and production requirements.
