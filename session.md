# Session Handoff — 2026-10-16T00:45:00Z

## Goal
Implement FR-6 (Informer Search & Alerting Service). Requirements + Workflow Planning approved; next step is Unit 1 (`LightningMcpServer`) Functional Design. Separately, EC2 Operations deployment connectivity appears resolved but root cause unconfirmed.

## Next actions (literal, copy-pasteable)
1. Resume AI-DLC: read `aidlc-docs/aidlc-state.md` and last 20 lines of `aidlc-docs/audit.md`.
2. Load `.aidlc-rule-details/construction/functional-design.md` plus `aidlc-docs/inception/requirements/requirements.md` FR-6 section.
3. Execute Unit 1 Functional Design for FR-6: define data models (search-match result, disambiguation candidate list, trimmed/grouped alert record) and business rules (fuzzy match on 4 fields, disabled-informer short-circuit, multi-match disambiguation, no-match/no-alerts messaging).
4. Present the standardized 2-option completion message (Request Changes / Continue to Next Stage) per `functional-design.md` — do not use a 3-option menu.
5. On approval, continue to Unit 1 NFR Requirements → NFR Design → Code Generation, then Unit 2 Functional Design → Code Generation, then Build and Test (see `aidlc-docs/inception/plans/execution-plan.md` Addendum: FR-6 for the full sequence).

## Context & Discovery
- User requested a real Informer search+alerts feature; existing `get_informer_status` MCP tool (`unit-1/src/LightningMcpServer/DomainModules/InformerStatusModule.cs`) was found to be fully randomized stub data, unrelated in shape to the new requirement.
- Inspected `data/informer/informer_list.json` (520KB, many informers, several customers have multiple informers — duplicate-match risk) and `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json` (alerts nested under multiple named sites `r[].nm`, each with an `a[]` alert array with fields `atn`/`m`/5 UTC timestamp fields).
- Checked `.env` for existing base-URL env var naming convention (`LIGHTNING_PULSE_API_BASE_URL`, `WEATHER_FORECAST_API_BASE_URL`) to match for new `INFORMER_API_BASE_URL`/`ALERTS_API_BASE_URL`.
- Requirements gathered via a 9-question clarification file, then one follow-up clarification question (Q8 disabled-informer behavior was genuinely ambiguous between two code paths) — both fully answered.
- Separately, discovered via `claude mcp list` that the `lightning-mcp` MCP server (`http://10.8.20.224:8020/mcp`, EC2 private IP) now shows Connected — a change from the 2026-10-08 audit log's "blocked by IAM-denied security-group change" state. User confirmed this is the correct live endpoint.

## Files touched
- `aidlc-docs/aidlc-state.md` — current stage updated to Unit 1 Functional Design (FR-6); EC2 connectivity resolution noted; Stage Progress rows added for Unit 1/Unit 2 FR-6 tracking.
- `aidlc-docs/audit.md` — appended: EC2 connectivity confirmation + `lightning-mcp-local` registration; FR-6 requirements request/questions/answers/clarification/approval; FR-6 Workflow Planning approval.
- `aidlc-docs/inception/requirements/requirements.md` — added `FR-6: Informer Search & Alerting Service` section.
- `aidlc-docs/inception/requirements/requirement-verification-questions.md` — 9 questions, all answered by user.
- `aidlc-docs/inception/requirements/requirement-verification-clarification-questions.md` — new file, 1 follow-up question, answered.
- `aidlc-docs/inception/plans/execution-plan.md` — added "Addendum: FR-6" section (phases to execute/skip, rationale, success criteria).
- `data/informer/informer_list.json` — new, QA stub data for informer search (contains real colleague emails and `pasUserSecreteKey` GUIDs — committed as-is, see Decisions).
- `data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json` — new, QA stub alerts data.
- `session.md` — this handoff.

## External Systems & Credentials
- EC2 instance `i-02f1106cb03f558e2`, public IP `54.91.14.149`, private IP `10.8.20.224` — `lightning-mcp` MCP server config now points at `http://10.8.20.224:8020/mcp` and connects successfully.
- AWS profile `qa` — IAM-denied from modifying security groups (`AEMDeniesDevelopers` policy); drafted SG ingress rule (TCP 8020/8001, VPN range + public IP) still not confirmed as the thing that fixed connectivity.
- Real endpoints for FR-6 (currently off-hours-unavailable, to be wired behind a `stub`/`live` config switch): `http://qa.oas.api.enqa.co/api/oas/Informer/v1/GetInformers`, `http://qa.alerts.api.enqa.co/api/alerts/v1/ByUserIdAndTimeRange`.
- GitHub repo (public): `github.com/Girish-Krishna-AEM/aem.mcp`.
- Local-only Claude Code MCP config (not in repo): second server `lightning-mcp-local` → `http://localhost:8020/mcp`, added this session for side-by-side EC2/local querying.

## Decisions
- FR-6 replaces `get_informer_status`'s tool contract entirely rather than adding a new tool name — prior contract was pure random stub with no real consumers.
- `INFORMER_DATA_SOURCE`/`ALERTS_DATA_SOURCE` = `stub`|`live` is an explicit config switch per endpoint — no automatic fallback-on-failure between stub and live in production code paths.
- Search matching is fuzzy/partial, case-insensitive, across all 4 fields (`custName`/`informName`/`macAddress`/`informId`).
- Disabled informers always appear in search matches; if the one ultimately selected is disabled, skip the alerts call and report "disabled, not operational" instead.
- Multi-match search results in a disambiguation pick-list shown to the user (reusing the FR-4 ambiguous-location pattern) — no auto-pick or auto-aggregate-all.
- Alerts response stays grouped by site name (`r[].nm`), not flattened.
- Per-alert response is trimmed to `atn`, `m`, and all 5 UTC timestamp fields (`actutc`/`issutc`/`exputc`/`acutc`/`lmutc`) — all other upstream fields dropped.
- Unit 2 reuses its existing local→UTC time-range parsing as-is; no new NFRs introduced in Unit 2 for this feature (all new external-API NFRs live in Unit 1).
- User Stories stage skipped for FR-6, consistent with the FR-4/FR-5 precedent (backend/internal feature) — user explicitly approved this.
- Committed `data/informer/*` stub files as-is (unredacted) to the public repo — user explicitly confirmed this QA data (real emails, GUID-shaped secret keys) is acceptable to commit after being flagged.

## Assumptions (unverified)
- The mechanism behind the EC2 connectivity fix (VPN routing to the private IP vs. an applied security-group rule) was not re-diagnosed — if it was VPN-dependent, disconnecting from VPN may break it again.
- `pasUserSecreteKey` values in the stub data are assumed non-live/non-sensitive based on the user's confirmation, not independently verified against the real system.

## Environment
- Branch: `main`. Working tree had the FR-6 doc changes + new `data/` directory staged for commit at session end (see commit hash below once created).
- `lightning-mcp-local` MCP server is registered in local Claude Code config but will show "Failed to connect" until a local `docker compose up` is run.

## Rollback point
- Last commit before this session's changes: `abd911e` ("Log EC2 security-group connectivity blocker in AI-DLC docs"). This session's doc + stub-data changes were committed on top — see repo log for the new commit hash.

## Risks/watch-items
- `data/informer/informer_list.json` (520KB) now permanently contains real colleague emails and GUID-shaped secret keys in public repo history.
- EC2 connectivity root cause unconfirmed — re-verify `claude mcp list` shows `lightning-mcp` Connected at the start of next session before relying on it.
- FR-6 implementation (Unit 1 Functional Design onward) has not started — no code changes yet, only requirements/planning docs.
