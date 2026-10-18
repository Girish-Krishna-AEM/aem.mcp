# Informer Result Formatting — Code Summary (FR-6, Unit 2)

## Modified files
- `unit-2/src/CoordinatorAgent/Services/ResultFormatter.cs` — `BuildInformerSummary` rewritten to branch on the tool response's `kind` field (`"Found"`/`"Disabled"`/`"Ambiguous"`/`"NotFound"`):
  - `Found`: informer name + total alert count across all site groups, or "no alerts in the requested time range" when zero.
  - `Disabled`: informer name + disabled notice.
  - `Ambiguous`: lists candidate informers (name/customer/MAC).
  - `NotFound`: generic not-found message.
  - Old `status`/`powerStatus` field reads removed (no longer part of the tool's response shape).
