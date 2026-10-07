# Business Rules — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

## BR-1: Geocoding Applicability
The `LocationGeocoder` MUST be invoked by any parameter-extraction path that needs to resolve a City+State, ZIP, or full-address string to coordinates. Currently this is the **Strike** and **Weather** intents (both route through `TryExtractLocation`). Any future intent requiring location resolution MUST reuse the same component rather than reimplementing lookup logic.

## BR-2: Explicit Coordinates Bypass Geocoding
If the query already contains explicit `lat`/`long` values (matched by the existing `CoordinatePattern` regex), geocoding is skipped entirely — the explicit coordinates are used as-is. This existing behavior is unchanged.

## BR-3: Single Match → Resolved
If the geocoder returns exactly one candidate match, treat it as authoritative. Return `GeocodeOutcome.Resolved` with that match's lat/lon and matched address.

## BR-4: Multiple Matches → Ambiguous, Never Auto-Select (Census only)
If the **Census Geocoder** returns more than one candidate match, do NOT automatically pick one (e.g. "first" or "best"). Return `GeocodeOutcome.Ambiguous` with the full list of candidate matched-address strings, and surface an error message to the user listing them (format: `"Multiple locations match '<input>'. Did you mean: <candidate 1>; <candidate 2>; ...? Please specify state or ZIP."`), so they can re-submit a more specific query. *(Corrected 2026-10-06 — scoped to Census only)*: this rule does NOT apply to the Nominatim fallback, which requests only its single top-ranked result (see BR-11.1) — live testing showed Nominatim returning several same-state sub-localities for simple queries like "Germantown MD", and surfacing those as a disambiguation prompt broke the project's own headline example.

## BR-5: No Match → Fallback, Then Not Found
*(Corrected 2026-10-06)* If the Census Geocoder returns zero candidate matches, fall back to Nominatim before concluding not-found (Census only resolves full street addresses — confirmed via live testing that it cannot match City+State or bare ZIP input at all). Only if Nominatim ALSO returns zero matches, return `GeocodeOutcome.NotFound` and surface a clear error (e.g. `"Could not resolve location '<input>'. Please provide a valid US city/state, ZIP code, or street address."`).

## BR-6: US-Only Scope
No explicit country filtering is applied beyond what the US Census Geocoder itself does (it is inherently US-only — non-US input will naturally resolve to `NotFound` since the provider has no non-US data). No separate validation step is needed for this.

## BR-7: Caching
Successful (`Resolved`), ambiguous, and not-found outcomes are ALL cached (keyed by lowercased+trimmed input string) with a TTL (value set in NFR Design). This avoids repeated external calls for the same input string within the cache window, including for inputs that are ambiguous or unresolvable (protects against repeated failed lookups, e.g. a user retrying the exact same bad input).

## BR-8: Radius Unit and Cap
- Radius unit (km/miles) detection from query phrasing is unchanged from existing behavior.
- After extraction, convert to miles for comparison and reject (validation error, e.g. `"Radius of <value> <unit> exceeds the maximum allowed radius of 100 miles."`) if the value exceeds 100 miles — applies uniformly regardless of whether the radius was explicitly stated or came from the existing default.

## BR-9: Retry on Transient Geocoder Failure
On a timeout or transient HTTP failure calling either geocoder (Census or, when it's the active fallback, Nominatim), retry up to 2 additional times (3 attempts total) with backoff (concrete timing fixed in NFR Design) before surfacing a clear service-unavailable-style error to the user — consistent with the existing `McpServerException`/`HttpRequestException` → `503` handling pattern already used in `QueryController` for MCP Server failures.

## BR-11: Nominatim Fallback Scope and Etiquette *(Added 2026-10-06)*
- Nominatim is called ONLY when Census Geocoder returns zero matches — never as a first attempt, and never when Census returns an ambiguous (>1) result.
- Requests to Nominatim MUST include a descriptive `User-Agent` header (its usage policy requires this) and restrict results to `countrycodes=us` (consistent with BR-6's US-only scope).
- **BR-11.1** *(Corrected 2026-10-06)*: the Nominatim request MUST limit results to its single top-ranked match (`limit=1`), trusting Nominatim's own relevance ranking, rather than fetching multiple candidates and treating them as ambiguous. Live testing showed this is necessary because Nominatim commonly returns several same-state sub-localities for a simple "City, State" query (e.g. 5 different Maryland places all named "Germantown" for "Germantown MD") — surfacing those as a disambiguation prompt is not useful and broke the project's own headline example query. Trade-off, confirmed with the user: a genuinely ambiguous bare place name spanning multiple states (e.g. "Springfield" with no state) will silently resolve to Nominatim's top-ranked guess rather than prompting the user — accepted as a reasonable trade-off for Phase 1.
- The existing cache (BR-7) applies identically regardless of which provider ultimately resolved the outcome — callers/tests cannot distinguish "resolved by Census" from "resolved by Nominatim fallback" from the cached `GeocodeOutcome` shape, by design (the distinction is an internal implementation detail).

## BR-10: Error Message Convention
All geocoding-related errors (ambiguous, not-found, over-cap radius) are surfaced as a single string message under the existing `{ "error": "<string>" }` response shape — no new structured response field is introduced (confirmed by user, Q2 answer A).
