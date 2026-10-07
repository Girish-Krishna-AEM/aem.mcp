# NFR Requirements — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

## Performance
- Each individual HTTP call to the US Census Geocoder (`/locations/onelineaddress`) MUST time out after **10 seconds**.
- Cache hits MUST short-circuit before any HTTP call — sub-millisecond, in-process dictionary lookup.

## Reliability / Resilience
- On timeout or transient HTTP failure, retry up to 2 additional times (3 attempts total), with a **fixed 500ms delay** between attempts (no exponential backoff — simplest to implement/reason about, consistent with the POC's overall simplicity level and the fact that Resiliency Baseline extension is not opted in).
- After all 3 attempts fail, surface a clear service-unavailable-style error to the user, consistent with the existing `McpServerException`/`HttpRequestException` → `503` pattern in `QueryController`.

## Caching
- Geocoded outcomes (Resolved, Ambiguous, and NotFound) are cached in-memory with a **1-hour TTL**.
- Cache is **unbounded** in size — acceptable for this POC given low expected traffic volume and that the process restarts (clearing the cache) align with normal container lifecycle; revisit if Phase 2 traffic assumptions change.

## Tech Stack / Integration
- `HttpClient` for Census Geocoder calls MUST be managed via **`IHttpClientFactory`** with a named/typed client registered in `Program.cs` (standard ASP.NET Core practice — avoids socket exhaustion under load, consistent with how `IMcpClient`'s `HttpClient` should already be, or now also be, managed).

## Testability
- All new tests for `LocationGeocoder` MUST be pure unit tests using a mocked `HttpMessageHandler`/`HttpClient` — no real network calls in the test suite. Cover: single match, ambiguous match, not-found, timeout-then-retry-succeeds, timeout-exhausts-retries, and cache-hit-skips-HTTP-call paths. This matches the existing `CoordinatorAgent.Tests` convention of mocking dependencies (no live-integration test added, per user's explicit choice — keeps CI deterministic and fast).

## Observability (POC-level, consistent with NFR-3 in requirements.md)
- Log (at `Information` level): cache hit/miss, each geocoder attempt (including retries), and the final outcome (Resolved/Ambiguous/NotFound) — sufficient for manual verification, matching the existing logging style in `ParameterExtractor`/`QueryController`.
- Log (at `Warning` level): ambiguous and not-found outcomes (mirrors existing `_logger.LogWarning("Missing parameter: ...")` pattern).
- Log (at `Error` level): final failure after retries exhausted.
