# Session Handoff — 2026-10-26T18:15:00Z

## Goal
FR-7 "Multi-Informer Location Search" feature designed/built/tested/deployed to EC2 and verified; then iterated a "Customer Rep" chat-response persona (always recommend, always combine lightning + hourly forecast) into `CLAUDE.md`. Session now closing.

## Next actions (literal, copy-pasteable)
1. Decide on committing the persona convention edit to `CLAUDE.md` (currently uncommitted, user was asked, no answer given before close):
   `cd C:\aem\ai-agentic\mcp && git add CLAUDE.md && git commit -m "Codify Customer Rep persona + always-recommend convention" && git push origin main`
2. If direct MCP tool access to the EC2 `lightning-mcp` server is wanted (vs. raw curl), reconnect/restart that MCP server registration in Claude Code so it picks up the new `get_informers_near_location` tool (its cached tool list predates the redeploy).
3. Check whether local Docker Compose containers are still running on this machine and stop if not needed: `cd C:\aem\ai-agentic\mcp && docker compose ps` then `docker compose down` if so.

## Context & Discovery
- Started by verifying `ALERTS_DATA_SOURCE` stub/live switch pre-deploy: found running local containers were stale (stub) vs `.env` (live) — fixed via `--force-recreate`, confirmed both modes work against the real `qa.alerts.api.enqa.co` endpoint.
- Built FR-7 via Plan Mode: 2 parallel Explore agents (Unit 1 data model incl. stub-data lat/lon reality check; Unit 2 query flow) → 1 Plan agent → 4 AskUserQuestion decisions (all "Recommended" accepted) → written plan approved via ExitPlanMode → implemented.
- During testing, found and fixed a real pre-existing bug: `IntentClassifier`'s Informer regex (`\binformer\b`) never matched plural "informers" (word-boundary fails mid-word on trailing "s") — natural phrasing like "find informers near X" silently failed to classify. Fixed to `\binformers?\b`.
- EC2 deploy-path confusion: initially inferred `~/aem-mcp` from shell history (wrong); confirmed the real path via `docker inspect coordinator-agent --format '{{index .Config.Labels "com.docker.compose.project.working_dir"}}'` → `/opt/lightning-mcp` (the script's default all along). Lesson: verify against running-system ground truth, not shell history alone.
- Ran a 16-scenario full smoke test directly against the live EC2 instance (`10.8.20.224`) covering every intent/outcome — all 16 passed, including the new location-search modes and a real-geocoding case that found a genuine nearby informer.
- Persona conventions were established iteratively through live user queries (Pampa TX, Plainview TX lightning+forecast checks) and then explicitly requested to be codified as a standing rule.

## Files touched
- `unit-1/src/LightningCommon/DomainModels.cs` — added `Latitude`/`Longitude`/`Radius` to `InformerRecord`
- `unit-1/src/LightningCommon/GeoDistanceUtils.cs` (new) — shared Haversine distance utility, extracted from `StrikeDetectionModule`
- `unit-1/src/LightningCommon/InformerLocationModels.cs` (new) — request/response/match types for location search
- `unit-1/src/LightningMcpServer/DomainModules/{IInformerStatusModule,InformerStatusModule,StrikeDetectionModule}.cs` — new `GetInformersNearLocationAsync` with parallel fan-out; Strike module now calls shared distance utility
- `unit-1/src/LightningMcpServer/Mcp/{McpTools,ToolRegistry}.cs` — registered new `get_informers_near_location` tool on both MCP surfaces
- `unit-1/tests/...` — `GeoDistanceUtilsTests.cs` (new), extended `InformerStatusModuleTests.cs`, both integration test files (7-tool count, new tool coverage)
- `unit-2/src/CoordinatorAgent/Services/{ParameterExtractor,IntentClassifier,ResultFormatter}.cs` — location-mode extraction, plural-informer regex fix, `BuildInformerListSummary`
- `unit-2/src/CoordinatorAgent/Controllers/QueryController.cs` — `ResolveInformerToolName` (mirrors existing `ResolveWeatherToolName` pattern)
- `unit-2/tests/...` — `ResultFormatterTests.cs` (new, first-ever for that class), extended `ParameterExtractorTests.cs`, `IntentClassifierTests.cs`, `CoordinatorIntegrationTests.cs`
- `aidlc-docs/aidlc-state.md`, `aidlc-docs/audit.md` — full FR-7 addendum, EC2 deploy-path correction, deploy + smoke-test entries
- `aidlc-docs/construction/unit-{1,2}/functional-design/business-rules.md` — new BR-15 through BR-20 (Unit 1), BR-15 through BR-18 (Unit 2)
- `CLAUDE.md` — **uncommitted**: Customer Rep persona + always-recommend + always-combine-lightning-and-forecast convention added to "Response Tone for Data-Query Answers" section
- `~/.claude/.../memory/feedback_customer_rep_persona_always_recommend.md` (new) + `project_lightning_poc_phase1_inception_complete.md` (refreshed) + `MEMORY.md` index — cross-session memory updates

## External Systems & Credentials
- GitHub: `github.com/Girish-Krishna-AEM/aem.mcp` (public), pushed commit `41ce6a9` on `main`
- EC2 instance: `i-02f1106cb03f558e2` / `10.8.20.224`, deployed at `/opt/lightning-mcp`, user `ec2-user`; containers `lightning-mcp-server` (:8020) and `coordinator-agent` (:8001), both healthy as of last check
- Real upstream QA APIs (all reachable from both local and EC2 as of this session): `qa.alerts.api.enqa.co`, `qa.oas.api.enqa.co`, `qa.lxneartime.api.enqa.co` (lightning pulse), `stg.en.ods.forecasts.web.enstg.co` (weather forecast)
- Local Docker Compose stack (`docker-compose.yml`) — may still be running on this machine, see Next Actions #3

## Decisions
- New dedicated `get_informers_near_location` tool (not an overload of `get_informer_status`) — keeps the existing single-search contract byte-for-byte frozen
- 100-mile radius cap + nearest-25 truncation, reusing Unit 2's existing Strike-intent cap constants
- Disabled informers included in location-search results but never alerts-queried (mirrors existing single-search short-circuit)
- Response carries counts only (`alertCount`/`siteCount`/`distanceMiles`), never full per-alert detail — token efficiency
- Parallel fan-out happens server-side in Unit 1 (one Unit2→Unit1 call); Unit 2 never makes N separate MCP calls
- One informer's Alerts-API failure during fan-out doesn't fail the whole request (reports 0 counts + logs a warning)
- Real EC2 deploy directory is `/opt/lightning-mcp`, confirmed via `docker inspect` label — not `~/aem-mcp`

## Assumptions (unverified)
- New tool's `radiusUnit` defaults to `"miles"` (vs. Strike's `"km"` default) — a deliberate but non-forced judgment call; could be changed to `"km"` for stricter consistency with Strike if that ever becomes a problem
- `LooksLikeLocationMode` (location phrase + radius phrase both present) may have untested edge cases beyond what the new test suite covers

## Rejected approaches
- Overloading `get_informer_status` with optional lat/lon params — rejected to avoid a dual-shape response contract and to keep BR-13 frozen
- N separate parallel Unit2→Unit1 MCP calls (one per informer) — rejected in favor of single-call, server-side fan-out in Unit 1

## Environment
- Branch `main`, in sync with `origin/main` at commit `41ce6a9` (FR-7 pushed and merged)
- `CLAUDE.md` has uncommitted local changes (persona convention) — not yet pushed
- This session's bash shell had `INFORMER_DATA_SOURCE`/`ALERTS_DATA_SOURCE` accidentally exported as `stub` at one point mid-session (from an earlier `VAR=x cmd` prefix leaking into the persistent shell) — if reusing this exact terminal, run `unset INFORMER_DATA_SOURCE ALERTS_DATA_SOURCE` before any `docker compose` command to avoid silently overriding `.env`

## Rollback point
Commit `41ce6a9` on `main` (pushed to GitHub) — last known-good, fully tested and EC2-deployed state before the uncommitted `CLAUDE.md` persona edit

## Open questions (blocked on user)
- Whether to commit + push the `CLAUDE.md` persona convention update to GitHub — asked, session closed before an answer was given

## Risks/watch-items
- `CLAUDE.md` persona edits are local-only — will be lost on a hard reset/fresh clone if not committed
- Local Docker containers may be left running on this machine
- The session's registered `lightning-mcp` MCP server connection has a stale (pre-redeploy) tool list — doesn't show `get_informers_near_location` for direct MCP tool use until reconnected
- Known pre-existing gap (unaffected by FR-7): FR-6's Disabled-informer short-circuit path is still only covered by mocked tests — no real `isDisabled: true` records exist in the stub data
