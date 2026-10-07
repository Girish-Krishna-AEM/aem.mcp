# Infrastructure Design Plan: Unit 3 — Infrastructure Orchestration

## Context

Units 1 and 2's infrastructure designs already fully specified the shared AWS topology (single EC2 `t3.small`, us-east-1, Amazon Linux 2023, 10 GB EBS gp3, one security group, Docker + Docker Compose) and the local/EC2-agnostic deployment approach. Unit 3 owns no new infrastructure decisions — its job is to consolidate those decisions into the actual orchestration artifacts (`docker-compose.yml`, `.env.example`, EC2 Deployment Guide, `DEPLOYMENT.md`), which happens in Code Generation. This Infrastructure Design stage records that consolidation as the formal design record for Unit 3.

## Plan Steps

- [ ] Step 1: Confirm all compute/networking/storage/runtime decisions are 100% inherited from Unit 1 & 2 infra designs — no new resources.
- [ ] Step 2: Resolve the one Unit-3-specific open question (EC2 provisioning automation level for Phase 1).
- [ ] Step 3: Generate `aidlc-docs/construction/unit-3/infrastructure-design/infrastructure-design.md`.
- [ ] Step 4: Generate `aidlc-docs/construction/unit-3/infrastructure-design/deployment-architecture.md`.
- [ ] Step 5: Present completion message and wait for approval.

## Context-Appropriate Questions

### Deployment Environment / Compute / Storage / Networking / Monitoring / Security
All already decided in Unit 1's infrastructure-design.md (reused by Unit 2). No new questions — Unit 3 inherits unchanged.
[Answer]: Confirmed — fully inherited, no changes.

### Shared Infrastructure
**EC2 provisioning automation**: Confirm Phase 1 stays with manual EC2 launch + manual Docker Compose deployment (per Unit 1's infra design, which explicitly deferred Terraform/CloudFormation to Phase 2), rather than introducing IaC now as part of Unit 3's orchestration scope.
[Answer]: Confirmed — manual provisioning for Phase 1; Unit 3's "EC2 Deployment Guide" documents the manual steps (already drafted in Unit 1's deployment-architecture.md) rather than introducing Terraform/CloudFormation, keeping Phase 1 scope minimal per the approved roadmap.
