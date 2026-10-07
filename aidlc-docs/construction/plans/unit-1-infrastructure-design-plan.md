# Infrastructure Design Plan: Unit 1 - Lightning MCP Server

## Context
Unit 1 (Lightning MCP Server) is a .NET 10 containerized service deployed to a single AWS EC2 instance via Docker Compose. This plan identifies infrastructure choices needed to operationalize the service.

## Identified Infrastructure Areas

From `requirements.md`:
- **NFR-2**: Containerization & Deployment (Docker, Docker Compose, single EC2 instance)
- **NFR-2 Open**: EC2 instance type, region, AMI/base OS, security group configuration not yet specified
- **NFR-3**: Observability (basic structured logs sufficient for POC)

## Critical Infrastructure Questions

### 1. **EC2 Instance Type & Sizing**

**Question 1.1**: What EC2 instance type and size should host the Phase 1 POC?
- **A) t3.small** (1 vCPU, 2 GB RAM, ~$0.025/hr) — Minimum viable for POC, sufficient for stub workload
- **B) t3.medium** (2 vCPU, 4 GB RAM, ~$0.05/hr) — More headroom, supports concurrent requests better
- **C) Other** (specify preferred instance type)

**[Answer 1.1]**:

---

### 2. **AWS Region & Base OS**

**Question 2.1**: Which AWS region should host the EC2 instance?
- **A) us-east-1** (N. Virginia, lowest cost, highest availability)
- **B) us-west-2** (Oregon)
- **C) eu-west-1** (Ireland)
- **D) Other** (specify region)

**[Answer 2.1]**:

**Question 2.2**: Which base OS/AMI should the EC2 instance use?
- **A) Amazon Linux 2023** (optimized for AWS, minimal, ~1 GB image, Docker pre-installable)
- **B) Ubuntu 22.04 LTS** (broader third-party tool support, slightly larger)
- **C) Other** (specify AMI preference)

**[Answer 2.2]**:

---

### 3. **Docker Storage & Disk Sizing**

**Question 3.1**: How much EBS disk volume should be attached to the EC2 instance?
- **A) 20 GB** (default gp3, sufficient for OS, Docker runtime, container images, logs for POC)
- **B) 50 GB** (extra headroom for future phases)
- **C) Other** (specify GB)

**[Answer 3.1]**:

---

## Default Assumptions (Validating Your Intent)

| Infrastructure Dimension | Default | Rationale |
|---|---|---|
| **Deployment Environment** | AWS EC2 (single instance, no auto-scaling) | Per Phase 1 requirements; POC scope |
| **Container Runtime** | Docker + Docker Compose | Per requirements; standard for single-machine orchestration |
| **Networking** | EC2 security group allowing SSH (22), HTTP (8000 for MCP Server), HTTP (8001 for Coordinator Agent) | Minimal, allows testing; Phase 2 adds API Gateway/WAF |
| **Storage Infrastructure** | No persistent storage (RDS, DynamoDB, S3) | Phase 1 uses only in-memory stub data |
| **Messaging** | N/A (HTTP/SSE inter-service communication via container network) | Determined during NFR Design |
| **Load Balancing** | None (single instance, single HTTP listener) | Phase 2 scales to multi-instance; adds load balancer then |
| **Observability** | Docker logs (stdout JSON), viewable via `docker compose logs` | Phase 1 scope; Phase 2 adds CloudWatch/ELK |
| **Monitoring & Alerting** | None (manual operator viewing logs) | POC-level; Phase 2 adds CloudWatch alarms |
| **DNS/Domain** | None (direct EC2 public IP or internal DNS) | POC scope; Phase 2 adds domain + Route 53 |
| **CI/CD** | Manual deployment (git pull, docker compose build/up) | POC-level; Phase 2 adds CodePipeline/CodeBuild |

---

## Summary of Design Plan Checkboxes

- [ ] **1.1**: Select EC2 instance type (t3.small or t3.medium recommended)
- [ ] **2.1**: Select AWS region (us-east-1 recommended for cost/availability)
- [ ] **2.2**: Select base OS (Amazon Linux 2023 or Ubuntu 22.04 LTS)
- [ ] **3.1**: Select EBS disk volume size (20 GB or 50 GB recommended)
- [ ] **Validate defaults**: Confirm assumptions about networking, storage, observability, monitoring, DNS, CI/CD
- [ ] Generate `infrastructure-design.md` with mapped components, decisions, and rationale
- [ ] Generate `deployment-architecture.md` with architecture diagram, service topology, and deployment steps
- [ ] Present completion message and wait for approval

---

**Next Step**: Please answer critical questions 1.1, 2.1, 2.2, and 3.1 above using the `[Answer N.N]` tags. Confirm or adjust any of the default assumptions. Once collected, I'll generate infrastructure design artifacts for your review.
