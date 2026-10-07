# Infrastructure Design: Unit 1 — Lightning MCP Server

## Overview

This document maps Unit 1's logical software components to actual AWS infrastructure services and deployment choices for the Phase 1 POC.

---

## 1. Compute Infrastructure: EC2 Instance

### Selected Configuration
- **Instance Type**: `t3.small` (1 vCPU, 2 GB RAM)
- **AWS Region**: `us-east-1` (N. Virginia)
- **Base OS**: Amazon Linux 2023
- **EBS Volume**: 10 GB `gp3` (general-purpose SSD)

### Rationale

**t3.small sizing**:
- Sufficient for Phase 1 POC with synthetic/stubbed data and no real I/O.
- Single vCPU supports sequential tool invocations; concurrency testing deferred to Phase 2.
- 2 GB RAM supports .NET 10 runtime + Docker daemon + container overhead.
- Lowest cost tier (`t3.small` ~$0.025/hr) suitable for POC; Phase 2 scales to `t3.medium` or `t3.large` if real workload demands increase.

**us-east-1 region**:
- Lowest AWS pricing; highest service availability and features.
- Suitable for POC; Phase 2 can add multi-region or geo-specific deployment if required.

**Amazon Linux 2023**:
- AWS-optimized minimal OS (~1 GB footprint), pre-configured with AWS tools (aws-cli, CloudWatch agent).
- Minimal attack surface, regular security patches.
- Docker and Docker Compose installable via `yum` without external repositories.

**10 GB EBS gp3**:
- 10 GB breakdown:
  - OS + system packages: ~3 GB
  - Docker runtime + images: ~4 GB (two containerized services)
  - Logs (7 days retention): ~1 GB
  - Buffer/headroom: ~2 GB
- Sufficient for POC; Phase 2 increases if log retention extends or services add stateful storage.

### Infrastructure Provisioning

**Manual provisioning for POC** (Infrastructure-as-Code deferred to Phase 2):
1. Launch EC2 instance: `t3.small`, Amazon Linux 2023 AMI, 10 GB `gp3` EBS volume, us-east-1a AZ.
2. Assign security group (see Networking section below).
3. Create Elastic IP (static public IP for EC2 instance).
4. SSH into instance; run user data script (see Deployment Steps).

**Future (Phase 2)**: Automate with Terraform or CloudFormation for reproducibility and multi-environment support.

---

## 2. Storage Infrastructure

### Data Storage: None (Phase 1)

**Rationale**: All tool responses are synthetic/stubbed; no persistent data storage (RDS, DynamoDB, S3) required.

**In-Memory State**: Lightning MCP Server is stateless; each request is independent. No session cache, request logs, or counters maintained in-memory across requests.

**Container Image Registry**: Docker images built locally on EC2 instance during deployment (`docker compose build`). No ECR (Elastic Container Registry) or external image repository for Phase 1.

**Logs Storage**: JSON logs written to stdout, captured by Docker daemon, accessible via `docker compose logs`. No external log aggregation (CloudWatch Logs, ELK) for Phase 1.

---

## 3. Networking Infrastructure

### Security Group Configuration

**Inbound Rules**:
| Protocol | Port | Source | Purpose |
|----------|------|--------|---------|
| SSH | 22 | Your IP or 0.0.0.0/0 (restrict to known IP range for security) | EC2 administration |
| HTTP | 8000 | 0.0.0.0/0 or specific subnet | MCP Server (external access for Coordinator Agent or testing) |
| HTTP | 8001 | 0.0.0.0/0 or specific subnet | Coordinator Agent (external access for testing) |

**Outbound Rules**:
| Protocol | Port | Destination | Purpose |
|----------|------|-------------|---------|
| All | All | 0.0.0.0/0 | Allow all outbound (for OS updates, Docker Hub, CloudWatch agent) |

### Network Topology

```
┌─────────────────────────────────────────────────────────┐
│  AWS VPC (default, us-east-1a)                          │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  EC2 Instance (t3.small, 10 GB)                 │   │
│  │                                                 │   │
│  │  ┌─────────────────────────────────────────┐   │   │
│  │  │  Docker Daemon                          │   │   │
│  │  │  ┌────────────────────────────────────┐ │   │   │
│  │  │  │  Container 1: Lightning MCP Server │ │   │   │
│  │  │  │  Port 8000 (HTTP/SSE)              │ │   │   │
│  │  │  └────────────────────────────────────┘ │   │   │
│  │  │  ┌────────────────────────────────────┐ │   │   │
│  │  │  │  Container 2: Coordinator Agent    │ │   │   │
│  │  │  │  Port 8001 (HTTP)                  │ │   │   │
│  │  │  └────────────────────────────────────┘ │   │   │
│  │  │                                         │   │   │
│  │  │  Docker Network (bridge)                │   │   │
│  │  │  Inter-container comm: hostname-based   │   │   │
│  │  │  (e.g., Coordinator calls               │   │   │
│  │  │   http://lightning-mcp-server:8000)     │   │   │
│  │  └────────────────────────────────────────┘   │   │
│  │                                                 │   │
│  │  Elastic IP: <public-ip> (assigned to instance)│   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│  Security Group: SSH (22), HTTP (8000, 8001)          │
│                                                         │
└─────────────────────────────────────────────────────────┘
        │
        │ Internet
        ▼
    User/Tester
    (connects via public IP:8001 to Coordinator Agent)
```

### DNS & Access

**Phase 1 POC**: Direct EC2 public IP address (or Elastic IP).
- Example: User connects to `http://<elastic-ip>:8001` to reach Coordinator Agent.

**Phase 2**: Add Route 53 DNS record pointing to Elastic IP or Application Load Balancer.

---

## 4. Container Runtime Infrastructure

### Docker & Docker Compose Setup

**Installation** (on EC2 instance startup):
```bash
#!/bin/bash
# Install Docker
sudo yum update -y
sudo yum install docker -y
sudo systemctl start docker
sudo systemctl enable docker

# Install Docker Compose (v2)
sudo yum install docker-compose-plugin -y

# Create application directory
sudo mkdir -p /opt/lightning-mcp
sudo chown ec2-user:ec2-user /opt/lightning-mcp

# Pull source code or create docker-compose.yml (deploy step)
```

**Container Images**:
- **Lightning MCP Server**: Built from `Dockerfile` (in Code Generation phase), based on `mcr.microsoft.com/dotnet:10.0-aspnet` (Microsoft .NET 10 runtime).
- **Coordinator Agent**: Built from `Dockerfile`, based on `mcr.microsoft.com/dotnet:10.0-aspnet`.

**Container Network**:
- Docker Compose creates a bridge network automatically.
- Containers communicate via container names as hostnames (e.g., `lightning-mcp-server` resolves to the MCP Server container).
- No manual DNS configuration needed.

---

## 5. Observability Infrastructure

### Logging (Phase 1 - Manual)

**Mechanism**: JSON structured logs written to stdout by both containers.

**Access**:
```bash
# View all logs
docker compose logs

# Follow logs in real-time
docker compose logs -f

# Filter by service
docker compose logs lightning-mcp-server
docker compose logs coordinator-agent

# Parse JSON logs (example with jq)
docker compose logs | jq 'select(.level == "Error")'
```

**Log Retention**: Docker daemon default (12.0 MB per container or older than 24 hours). Phase 2 can add log aggregation or ECS CloudWatch integration.

### Monitoring & Alerting (Phase 1 - None)

**Deferred to Phase 2**: CloudWatch metrics, alarms, dashboards, email/SNS notifications.

**Phase 1 Manual Checks**:
- Health check endpoint: `curl http://<elastic-ip>:8000/health` (returns `{"status": "healthy"}`).
- Container status: `docker compose ps` (shows running/stopped status).
- Container logs: `docker compose logs` (manual inspection).

---

## 6. Security Infrastructure

### Encryption & Secrets

**Phase 1 - None**: No secrets (database credentials, API keys, PII) handled. All data is synthetic.

**Phase 2**:
- Environment variables for API keys, database URLs (injected via Docker Compose secrets or .env file).
- RDS encryption, S3 encryption for real data sources.

### Access Control

**SSH Access**: Security group restricts SSH to known IP ranges (recommended); restrict further in Phase 2.

**Application-Level Auth**: None (Phase 1 requirement, deferred to Phase 2 Edge Layer / Authentication).

---

## 7. High Availability & Disaster Recovery

**Phase 1 POC**: Single-instance, single-AZ deployment. No redundancy.

**Availability**: Downtime during updates/reboots. Acceptable for POC; Phase 2 adds multi-AZ with auto-scaling.

**Data Loss**: None (no persistent data). Phase 2 with databases adds backup/restore plans.

**Backup**: Manual snapshots of EBS volume (optional, for configuration preservation).

---

## 8. Cost Estimation (Phase 1 POC)

| Resource | Hourly | Monthly (730h) | Notes |
|----------|--------|---|---|
| **EC2 t3.small** | $0.0252 | ~$18.40 | us-east-1, on-demand |
| **EBS 10 GB gp3** | $0.10/GB/mo | $1.00 | One-time, minimal |
| **Data Transfer** | negligible | <$1 | Local testing, minimal external data transfer |
| **Total** | ~$0.025/h | ~$20/month | Minimal cost for POC; Phase 2 scales if needed |

---

## 9. Infrastructure as Code (Deferred to Phase 2)

**Current Approach**: Manual EC2 launch, manual Docker Compose deployment (suitable for POC).

**Phase 2 Automation**: Terraform or CloudFormation for:
- Repeatable EC2 provisioning
- Security group rules as code
- AMI + EBS snapshot management
- Multi-environment (dev/staging/prod) templates

---

## Summary of Infrastructure Decisions

| Component | Choice | Rationale |
|---|---|---|
| **Cloud Provider** | AWS | Requirement; Phase 1 scoped to single EC2 |
| **Compute** | EC2 t3.small, us-east-1a | Minimal cost, sufficient for stub workload |
| **OS** | Amazon Linux 2023 | AWS-optimized, minimal, Docker-ready |
| **Storage** | 10 GB EBS gp3, no persistent databases | POC uses only in-memory stub data |
| **Container Runtime** | Docker + Docker Compose | Orchestrates two services on single instance |
| **Networking** | Single EC2 + Security Group (SSH/HTTP ports) | Minimal network topology for POC |
| **Observability** | Docker logs (stdout JSON) | Manual inspection sufficient for POC |
| **Monitoring/Alerting** | None (Phase 1) | Manual health checks; Phase 2 adds CloudWatch |
| **HA/DR** | None (single instance) | Acceptable for POC; Phase 2 adds multi-AZ |
| **Cost** | ~$20/month | Minimal for proof-of-concept |

---

**Next**: See `deployment-architecture.md` for step-by-step deployment instructions and architecture diagrams.
