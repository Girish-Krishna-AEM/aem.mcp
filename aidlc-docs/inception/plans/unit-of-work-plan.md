# Unit of Work Plan: Lightning Detection MCP POC (Phase 1)

## Mandatory Artifacts

- [ ] Generate `aidlc-docs/inception/application-design/unit-of-work.md` (unit definitions, responsibilities, boundaries, code organization strategy for greenfield multi-unit project)
- [ ] Generate `aidlc-docs/inception/application-design/unit-of-work-dependency.md` (dependency matrix, integration contracts)
- [ ] Generate `aidlc-docs/inception/application-design/unit-of-work-story-map.md` (functional requirements FR-1.1 through FR-2 mapped to units)
- [ ] Validate unit boundaries against requirements (FR-1 tools, FR-2 agent logic, NFR-2/3 infrastructure)
- [ ] Ensure all functional requirements (FR-1.1–FR-1.4, FR-2) and NFRs (NFR-2, NFR-3) assigned to appropriate units
- [ ] Document code organization strategy for greenfield multi-unit .NET 10/C# project (directory structure, project layout, shared libraries)

## Proposed Unit Decomposition (from Execution Plan)

Per the approved Execution Plan (Section "Units to Execute"), Phase 1 decomposes into **3 units**:

### Unit 1: Lightning MCP Server Unit
- **Scope**: Implements FR-1 — MCP server exposing 4 stub tools
  - FR-1.1: `get_lightning_strikes_near_location`
  - FR-1.2: `get_weather_forecast`
  - FR-1.3: `get_sensor_diagnostics`
  - FR-1.4: `get_informer_status`
- **Responsibility**: MCP protocol transport, tool schema compliance, stub data generation, input validation, logging, health-check endpoint
- **Technology**: .NET 10.0/C#, MCP SDK, dependency injection, structured logging

### Unit 2: Coordinator Agent Unit
- **Scope**: Implements FR-2 — Agent service for intent routing and tool invocation
- **Responsibility**: Natural-language query parsing, intent classification (map query to one of 4 tools), parameter extraction, MCP server invocation, result formatting, missing-parameter handling, HTTP/CLI interface, logging, health-check endpoint
- **Technology**: .NET 10.0/C#, LLM integration (Claude SDK or similar), MCP client, HTTP server (ASP.NET Core), structured logging

### Unit 3: Infrastructure Orchestration Unit
- **Scope**: Implements NFR-2 (Containerization & Deployment) — Docker Compose orchestration and EC2 deployment patterns
- **Responsibility**: Dockerfile definitions (both services), docker-compose.yml orchestration, service networking, health-check configuration, environment variable management, EC2 deployment patterns (instance type, AMI, security groups, EC2 instance startup)
- **Technology**: Docker, Docker Compose, AWS EC2 (IaC or manual configuration recommendations)

---

## Decomposition Questions

The following questions are designed to clarify ambiguities and ensure unit boundaries align with implementation and organizational needs. Each question is marked with a category and followed by an explanation of why it matters to decomposition.

### A. Story Grouping & Unit Boundaries

#### Question A1: MCP Tool Granularity within Unit 1
**Question**: The 4 MCP tools (FR-1.1–FR-1.4) are currently grouped as a single **Lightning MCP Server Unit**. Each tool has distinct inputs, outputs, and stub logic (strikes proximity, weather forecast, sensor diagnostics, informer status). Should these 4 tools be sub-units (4 separate service units, each deployed independently), or is it appropriate to keep them grouped as a single MCP Server unit that exposes all 4 tools from a single .NET project?

**Why this matters**: This affects code organization (1 project vs 4 projects), deployment model (1 docker image vs 4), infrastructure complexity, and scalability model. If tools should be independently deployable, each becomes a micro-service. If grouped, they share a single codebase and image.

[Answer]: 4 distinct projects with 4 dockers - In real scenario these are 4 different service components

---

#### Question A2: Coordinator Agent as Single Monolithic Unit
**Question**: The Coordinator Agent is currently proposed as a **single, monolithic unit**. Its responsibilities are:
- Parse natural-language query
- Classify intent (map to one of 4 tools)
- Extract parameters
- Invoke correct MCP tool
- Return result with optional natural-language framing
- Handle missing parameters

FR-2 specifies "Phase 1: simple clarification, not full dialogue management." Should the Coordinator Agent remain a single unit, or would it benefit from sub-unit decomposition (e.g., Intent Classification module, Parameter Extraction module, Tool Invocation Client module)? If sub-units, should they be separate projects or logical modules within a single project?

**Why this matters**: This affects how the Coordinator is structured for testability, reuse, and future extension (Phase 2+). Single unit = simpler to deploy but harder to test in isolation; multiple modules = clearer responsibilities but more complex coordination.

[Answer]: Simple Clarification with this POC

---

#### Question A3: Mapping of All Functional Requirements to Units
**Question**: Verify that all functional and non-functional requirements map clearly to the 3 proposed units:
- **FR-1.1–FR-1.4** → Unit 1 (Lightning MCP Server) ✓
- **FR-2** (Coordinator Agent logic) → Unit 2 (Coordinator Agent) ✓
- **FR-3** (Interfaces: MCP protocol, HTTP endpoint) → Unit 1 + Unit 2 (protocol choice affects both) ?
- **NFR-2** (Containerization, Docker Compose) → Unit 3 (Infrastructure) ✓
- **NFR-3** (Logging, health checks) → Unit 1 + Unit 2 (each service implements) + Unit 3 (orchestration defines health-check behavior) ?

Are there any requirements that do NOT clearly map to one of these 3 units, or that span multiple units in a way that requires explicit integration contracts?

**Why this matters**: Ensures no functional gaps fall between units, and clarifies shared concerns (like health-check design) that must be coordinated across units.

[Answer]: 

---

### B. Dependencies & Integration

#### Question B1: MCP Protocol Transport Choice
**Question**: Requirements FR-3 states the MCP Server "MUST expose its 4 tools over the standard MCP protocol (stdio or HTTP/SSE transport — implementer's choice, document the choice in Construction phase)."

Does the choice of MCP transport (stdio vs HTTP/SSE) affect unit boundaries or decomposition? Specifically:
- If **stdio** is chosen: Coordinator Agent must launch MCP Server as a subprocess; tight coupling, shared process model.
- If **HTTP/SSE** is chosen: MCP Server and Coordinator Agent are loosely coupled via HTTP; independent processes/containers, network-based communication.

Which transport is preferred for Phase 1, and does it affect how you envision the 3 units being deployed/scaled?

**Why this matters**: Transport choice determines whether the units are tightly coupled (subprocess) or loosely coupled (network), affecting deployment independence, testing strategy, and inter-unit communication contracts.

[Answer]: loosely coupled via HTTP

---

#### Question B2: Shared Utilities and Cross-Cutting Concerns
**Question**: Both Unit 1 (MCP Server) and Unit 2 (Coordinator Agent) must implement:
- **Logging**: Structured, console-based logs (NFR-3) with consistent format across both services
- **Health checks**: Liveness/readiness endpoints for Docker Compose orchestration (NFR-3)
- **Dependency injection**: Configuration, logging setup, MCP client initialization
- **Error handling**: Consistent error response formats

Should these cross-cutting concerns be:
1. **Implemented independently** in each unit (duplication, but loose coupling)?
2. **Extracted to a shared library** (e.g., `LightningCommon` NuGet package) that both units depend on?
3. **Implemented as a base/template** that each unit instantiates?

If a shared library is preferred, should it be a separate unit of work, or part of the Infrastructure Orchestration unit?

**Why this matters**: Affects code reuse, testing complexity, deployment artifacts, and version management across services. Shared library introduces a dependency; independent implementations are simpler but create maintenance burden.

[Answer]: Shared Lib for this POC

---

#### Question B3: Inter-Unit Communication Contract
**Question**: Unit 2 (Coordinator Agent) must invoke Unit 1 (Lightning MCP Server) via the MCP protocol. This requires:
- Clear MCP tool schema definitions (inputs, outputs, error formats)
- MCP client implementation in Coordinator
- MCP server implementation in Lightning Server
- Service discovery/connectivity (how does Coordinator know where to reach the Server?)

Should the MCP tool schemas and communication contract be:
1. **Documented in a shared specification** (e.g., `aidlc-docs/schemas/mcp-tools.json`) that both units reference?
2. **Generated from a single source-of-truth** (e.g., shared .proto or OpenAPI spec) that generates both client and server code?
3. **Defined independently** in each unit's code (duplicated schemas)?

How should service discovery work in docker-compose (DNS name, hardcoded hostname, environment variable)?

**Why this matters**: Affects testing independence (can Unit 2 be tested without Unit 1?), maintenance burden (schema duplication vs centralized), and Docker Compose networking assumptions.

[Answer]: What is best recommendation for this POC [OpenAPI Spec] - Recommendation needed

---

#### Question B4: Unit 3 (Infrastructure) as Independent Unit
**Question**: Infrastructure Orchestration is proposed as a separate unit, responsible for Docker Compose and EC2 deployment patterns. However, Unit 1 and Unit 2 must produce Dockerfile and docker-compose.yml-compatible artifacts. Should:
1. **Unit 1 & 2** produce Dockerfiles and project structure, and **Unit 3** owns the docker-compose.yml and EC2 patterns?
2. All three units **jointly own** infrastructure (Dockerfile responsibility in Unit 1 & 2; orchestration in Unit 3)?
3. Infrastructure be a **separate, loosely-coupled unit** that integrates the outputs of Unit 1 & 2 without modifying them?

Are there any architectural constraints that would affect this split?

**Why this matters**: Determines whether Infrastructure is an integration layer (bringing existing units together) or a domain unit with its own responsibilities, affecting deployment independence and modification patterns.

[Answer]: Need Recommendation

---

### C. Team Alignment & Ownership

#### Question C1: Team Structure and Unit Ownership
**Question**: Will a single team own all 3 units, or will different teams own different units? Specifically:

1. **Single team ownership**: One team builds MCP Server, Coordinator Agent, and Infrastructure together; simpler coordination, tighter coupling.
2. **Divided ownership**: 
   - Team A owns Unit 1 (MCP Server)
   - Team B owns Unit 2 (Coordinator Agent)
   - Team C owns Unit 3 (Infrastructure)
3. **Hybrid ownership**: 
   - One team owns both Unit 1 & 2 (business logic)
   - Another team owns Unit 3 (infrastructure)

This affects the formality of inter-unit contracts, testing/deployment coordination, and code review processes.

**Why this matters**: Affects how strictly you define unit boundaries and inter-unit contracts. Single team can be informal; multiple teams require explicit contracts and handoff protocols.

[Answer]: Hybrid Ownership

---

#### Question C2: Deployment Independence
**Question**: Can Unit 1 (Lightning MCP Server) and Unit 2 (Coordinator Agent) be deployed independently? That is:
- Can you deploy a new version of the MCP Server to production without re-deploying the Coordinator Agent?
- Can you deploy a new version of the Coordinator Agent without touching the MCP Server?

Or must they always be deployed together (single docker-compose.yml, single EC2 instance, locked version pins)?

This affects the degree of coupling acceptable in the unit design (shared libraries, version compatibility, API contracts).

**Why this matters**: Independent deployment requires stricter backward-compatibility guarantees and versioning schemes; tight coupling allows more flexibility. Affects testing strategy and release cycles.

[Answer]: 

---

### D. Technical Considerations

#### Question D1: Scalability and Deployment Model
**Question**: Requirements state deployment target is "a single AWS EC2 instance running Docker + Docker Compose. No auto-scaling, load balancing, or multi-instance orchestration (e.g. ECS/EKS) is in scope for Phase 1."

For Phase 1, should the 3 units assume:
1. **Single-instance model**: Both services run on the same EC2 instance (docker-compose on one box); no separate scaling per service.
2. **Independently scalable services**: Architecture designed to support future independent scaling (e.g., multiple Coordinator instances, single MCP Server), even if Phase 1 deploys only 1 instance each.
3. **Horizontally replicated services**: Design for running multiple instances of a service on the same EC2 (e.g., multiple Coordinator containers behind a reverse proxy), for Phase 1 redundancy?

This affects service communication design (point-to-point vs load-balanced), state management (stateless vs stateful), and docker-compose networking.

**Why this matters**: Affects architecture decisions made now that either enable or constrain Phase 2 scaling patterns. Stateless design is safer for future scaling.

[Answer]: 

---

#### Question D2: Testing Strategy per Unit
**Question**: How should testing be structured across the 3 units?

1. **Unit-level testing**: Each unit has independent unit tests (no mocking of other units); Unit 1 tests tool logic in isolation, Unit 2 tests intent routing/parameter extraction in isolation, Unit 3 has no tests.
2. **Integration testing**: Unit 1 & 2 are tested together (Coordinator invokes MCP Server over MCP protocol) in a separate integration test suite.
3. **End-to-end testing**: Full docker-compose stack is tested (services start, health checks pass, full query flow works).
4. **Per-unit test ownership**: Each unit owns its own tests; integration tests owned separately; e2e tests owned by who?

Should there be a separate integration test project that spans units, or should each unit test only its own responsibilities?

**Why this matters**: Affects test artifact organization, test data/mocks, CI/CD pipeline structure, and who's responsible for cross-unit testing.

[Answer]: 

---

#### Question D3: Monitoring and Observability Patterns
**Question**: NFR-3 requires "basic structured console/stdout logs (startup, request received, tool invoked, tool result/stub returned, errors)" and "basic health-check endpoint" for each service.

Should these observability artifacts be:
1. **Independently implemented** in Unit 1 & 2 (each service defines its own logging format, health-check endpoint)?
2. **Standardized via a shared utility library** (consistent logging format, common health-check pattern)?
3. **Defined in Infrastructure Orchestration** (Unit 3 specifies the logging/health-check contract that Unit 1 & 2 must implement)?

Should docker-compose.yml include health-check definitions (unit 3), or should each Dockerfile define health checks internally (units 1 & 2)?

**Why this matters**: Affects log aggregation strategy (can logs be parsed uniformly?), debugging experience, and operational monitoring consistency.

[Answer]: 

---

### E. Business Domain & Context

#### Question E1: Future Domain Expansion
**Question**: Requirements note the 3-Phase Roadmap. Phase 2 will add real data integrations and a Session Store. Phase 3 may introduce additional domain agents (beyond Lightning) following the reference architecture pattern (Accounts, Transactions, Service domains, each with their own MCP Server).

Should the Phase 1 unit boundaries for Unit 1 (Lightning MCP Server) and Unit 2 (Coordinator Agent) be designed to:
1. **Support multiple Lightning-specific sub-domains**: E.g., Coordinator eventually routes to multiple MCP servers (Lightning-Strikes, Lightning-Weather, Lightning-Sensors, each as separate micro-services)?
2. **Support multiple domain agents**: Coordinator becomes a true "multi-agent dispatcher" that routes queries to any domain (Lightning, Accounts, Transactions, etc.), with each domain having its own Coordinator or aggregation point?
3. **Remain Lightning-specific for Phase 1**, with Phase 2/3 refactoring as needed?

This affects whether the Coordinator Agent is designed as general-purpose or Lightning-specific.

**Why this matters**: Early decision about whether Coordinator is reusable/generalizable (loose coupling, generic routing logic) vs domain-specific (tight integration with Lightning domain). Affects code structure, configuration, extensibility.

[Answer]: 

---

#### Question E2: Sensor/Device Domain Boundaries
**Question**: The Lightning MCP Server exposes tools for:
- **Strike/Proximity** (FR-1.1): Lightning strike detection
- **Weather Forecast** (FR-1.2): Weather domain integration
- **Sensor Diagnostics** (FR-1.3): Device health domain
- **Informer Status** (FR-1.4): Warning device domain

All 4 are grouped under "Lightning MCP Server" unit. However, could they represent distinct bounded contexts (Strike Detection domain, Weather domain, Device Management domain, Warning domain)?

Should Phase 1 design Unit 1 to:
1. **Keep all 4 tools in single MCP Server** (simple, monolithic, Phase 1 appropriate)?
2. **Pre-structure for domain separation** (e.g., internal modules per domain, preparation for Phase 2 micro-service split)?
3. **Separate the 4 tools into 4 domain-specific MCP servers** now (over-engineering for Phase 1, but cleaner for Phase 3)?

**Why this matters**: Affects code organization within Unit 1 (monolithic vs modular), future Phase 2/3 refactoring effort, and maintainability as real integrations are added.

[Answer]: 

---

### F. Code Organization (Greenfield Multi-Unit)

#### Question F1: Repository and Project Structure
**Question**: For this greenfield multi-unit project, should the code organization be:

1. **Monorepo** (single git repo with subdirectories per unit):
   ```
   mcp/
   ├── src/
   │   ├── LightningMcpServer/
   │   │   ├── LightningMcpServer.csproj
   │   │   ├── Tools/
   │   │   └── ...
   │   ├── CoordinatorAgent/
   │   │   ├── CoordinatorAgent.csproj
   │   │   ├── IntentClassifier/
   │   │   └── ...
   │   └── LightningCommon/ (optional shared library)
   ├── docker-compose.yml
   ├── docker/ (Dockerfiles)
   └── ...
   ```

2. **Polyrepo** (separate git repositories per unit):
   ```
   lightning-mcp-server/ (separate repo)
   coordinator-agent/ (separate repo)
   infrastructure-orchestration/ (separate repo or in coordinator-agent)
   ```

3. **Hybrid** (monorepo with separate build/deployment per unit):
   ```
   mcp/ (monorepo)
   ├── src/
   │   ├── LightningMcpServer/
   │   └── CoordinatorAgent/
   ├── build/ (separate build scripts per unit)
   └── ...
   ```

Monorepo is simpler for Phase 1; polyrepo allows independent versioning/releases for future phases.

**Why this matters**: Affects versioning strategy, CI/CD pipeline structure, dependency management, and team collaboration model. Monorepo is simpler now; polyrepo enables independent evolution later.

[Answer]: 

---

#### Question F2: Shared Code Organization
**Question**: If shared utilities (logging, health checks, common models) are needed across Unit 1 and Unit 2, where should they live?

1. **Shared NuGet package** (e.g., `LightningCommon` or `MCP.Common`):
   - Versioned independently
   - Published to NuGet feed or local package directory
   - Both Unit 1 & 2 depend on it as a package reference
   - Separate testing and deployment cycle

2. **Shared project within monorepo** (sibling .csproj to both units):
   - Simple to edit and test together
   - No separate versioning; all units versioned as one
   - Simpler for Phase 1; harder to decouple later

3. **Replicated code** (copy-paste or templating):
   - No shared dependency; each unit self-contained
   - Maximum coupling isolation; maximum maintenance burden

4. **No shared code** (each unit implements independently):
   - Maximum autonomy; maximum duplication

Which approach fits Phase 1 and prepares for Phase 2 extensibility?

**Why this matters**: Affects deployment complexity, version management, and how easily Phase 2 can extend shared patterns (logging, health checks) to additional agents/services.

[Answer]: 

---

#### Question F3: Build and Deployment Artifacts
**Question**: How should the build and deployment artifacts be organized?

1. **Single docker-compose.yml** (Unit 3 produces one file that orchestrates both services):
   - Simple for Phase 1 single-EC2 deployment
   - All services deployed/scaled together

2. **Multiple docker-compose files** (one per unit, or one per environment):
   ```
   docker-compose.yml (base/development)
   docker-compose.prod.yml (production overrides)
   docker-compose.unit1.yml (MCP Server only)
   docker-compose.unit2.yml (Coordinator only)
   ```
   - More flexibility for independent deployment

3. **Kubernetes/Helm** (over-kill for Phase 1):
   - Deferred to Phase 2+

4. **Infrastructure-as-Code** (Terraform/CloudFormation for EC2):
   - Simple bash/CloudFormation template for EC2 launch
   - Or manual console steps documented in README

Which approach is appropriate for Phase 1 and extensible for Phase 2?

**Why this matters**: Affects deployment complexity, repeatability, and whether Unit 1 & 2 can be deployed independently or must be orchestrated together.

[Answer]: 

---

#### Question F4: Build Process and CI/CD
**Question**: Should the build process be:

1. **Single build pipeline** (e.g., GitHub Actions / Azure Pipelines that builds both services, runs all tests, pushes both images):
   - Simple coordination; all units tested/released together
   - Slower feedback loop if only one unit changes

2. **Per-unit build pipelines** (separate CI for Unit 1, Unit 2, Unit 3):
   - Faster feedback; parallel builds
   - Requires explicit dependency/versioning coordination

3. **Monorepo with per-unit workflows** (single repo, but jobs triggered by path filters):
   ```
   trigger:
     - src/LightningMcpServer/ → build Unit 1
     - src/CoordinatorAgent/ → build Unit 2
   ```

Which approach is preferred for Phase 1, and does it affect unit boundaries?

**Why this matters**: Affects deployment velocity, test feedback time, and whether units can iterate independently.

[Answer]: 

---

---

## Summary of Decomposition Context

| Factor | Current State |
|--------|---------------|
| **Proposed Units** | 3: MCP Server, Coordinator Agent, Infrastructure Orchestration |
| **Technology** | .NET 10.0/C# (both services), Docker, Docker Compose, EC2 |
| **Deployment Target** | Single EC2 instance, docker-compose up |
| **Service Communication** | MCP protocol (stdio or HTTP/SSE — not yet decided) |
| **Data Model** | Stub/synthetic only; no persistence |
| **Team Structure** | Not yet specified |
| **Testing Strategy** | Not yet specified |
| **Code Organization** | Greenfield; no existing structure to follow |
| **Future Phases** | Phase 2: real data integrations + Session Store; Phase 3: complete reference architecture (Edge, Auth, Observability, etc.) |

---

## Approval Gate

### Instructions for Completing Decomposition Plan

1. **Review** each question above (A1–F4, total 15 questions across 6 categories)
2. **Fill in** the `[Answer]:` field for each question with:
   - Your preferred approach/choice
   - Rationale (1–2 sentences) for why you chose that option
   - Any constraints or assumptions (e.g., "This assumes single team ownership")
3. **Check for ambiguities**: If your answer contains "mix of," "somewhere between," "not sure," or "depends," clarify the decision rule
4. **Save** the completed plan
5. **Notify** when ready for review

### Review Criteria

Before approving this plan for **Part 2 (Generation)**, ensure:

- [ ] All 15 questions have been answered (no blank `[Answer]:` fields)
- [ ] Answers are specific and actionable (not vague or "depends")
- [ ] No contradictions between related questions
- [ ] Answers align with requirements and execution plan
- [ ] Unit boundaries are clear and non-overlapping
- [ ] Dependencies between units are explicit

---

**Plan Status**: Ready for user input on decomposition questions.
