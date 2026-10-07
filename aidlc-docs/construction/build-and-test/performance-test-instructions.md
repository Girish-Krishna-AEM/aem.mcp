# Performance Test Instructions

## Purpose

Confirm the Phase 1 POC meets its one informal performance expectation. There are no formal SLAs for Phase 1 (per `aidlc-docs/inception/requirements/requirements.md`, NFR-6) since all tool responses are synthetic/stubbed with no real external I/O — this is a lightweight sanity check, not a load-testing exercise.

## Performance Requirements (as documented, NFR-6)

- **Response Time**: Sub-second for tool calls (informal target; no formal SLA for Phase 1)
- **Throughput**: Not specified — POC is not expected to handle production load
- **Concurrent Users**: Not specified
- **Error Rate**: Not specified

## Setup

No special environment setup required — the stack as deployed via `docker compose up -d` is sufficient; no load balancer or scaled replicas exist in Phase 1.

## Run Performance Tests

### 1. Manual Latency Spot-Check (sufficient for Phase 1)

```bash
# Repeat for each of the 4 MCP tools via the Coordinator, and directly against Unit 1
time curl -s -X POST http://localhost:8001/query -H "Content-Type: application/json" -d '{"query": "Is there lightning near Austin, TX?"}' > /dev/null
time curl -s -X POST http://localhost:8000/mcp/messages -H "Content-Type: application/json" -d '{"method":"tools/call","params":{"name":"get_lightning_strikes_near_location","arguments":{"latitude":30.2672,"longitude":-97.7431,"radius":50,"radiusUnit":"km"}}}' > /dev/null
```

**Pass criteria**: `real` time reported by `time` is well under 1 second for each call (stub data generation is in-memory with no I/O, so this should be met trivially).

### 2. Load/Stress Testing

**Not performed for Phase 1** — out of scope per the requirements document (no formal SLA, synthetic data, single-instance POC). If Phase 2 introduces real data source integrations or production traffic expectations, add a proper load-testing step here (e.g., `k6` or `autocannon` against both `/query` and `/mcp/messages`) with defined throughput/latency/error-rate targets at that time.

## Performance Optimization

Not applicable for Phase 1 — no bottlenecks are expected (no real I/O, no database, in-memory deterministic stub generation). Revisit once Phase 2 replaces stubs with real external API calls.
