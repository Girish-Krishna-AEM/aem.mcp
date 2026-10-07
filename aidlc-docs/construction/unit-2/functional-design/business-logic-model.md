# Business Logic Model — Unit 2 (Coordinator Agent) — FR-4 Location Geocoding Utility

## Overview
A new `LocationGeocoder` component resolves any free-text location string (City+State, ZIP, or full street address) to a `GeocodeOutcome`. It is a **general-purpose utility**, not tied to a single intent: per the user's Q5 answer, it must be invoked by any current or future parameter-extraction path that needs to turn a city/state/ZIP/address string into coordinates — today that means the **Strike** and **Weather** intents, which both currently call the same `TryExtractLocation` method.

## Component: `LocationGeocoder`

### Responsibility
Given a raw location string, return one of: a resolved lat/lon (`Resolved`), a list of ambiguous candidates (`Ambiguous`), or no match (`NotFound`).

### Processing Flow
1. Normalize the input string for cache lookup (lowercase + trim — Q3 answer A).
2. Check the in-memory cache; if a non-expired entry exists, return its cached `GeocodeOutcome` immediately (no external call).
3. If not cached, call the US Census Geocoder's `/locations/onelineaddress` endpoint (Q1 answer A — single endpoint, pass the raw text through as-is; no format pre-detection/routing), with up to 3 total attempts (1 initial + 2 retries) on timeout/transient failure, with backoff between attempts (concrete values fixed in NFR Design).
4. **(Corrected 2026-10-06 — fallback added)** Map the Census response:
   - **Exactly 1 match** → `GeocodeOutcome.Resolved` with that match's coordinates + matched address (Census is address-range-based, so multiple Census matches are rare in practice, but handled the same as step 5 below if they occur)
   - **>1 matches** → `GeocodeOutcome.Ambiguous` with the list of matched address strings — no Nominatim fallback in this case (Census already found candidates)
   - **0 matches** → Census could not resolve it (confirmed via live testing: this is the normal outcome for City+State or bare-ZIP input, since Census only matches full street addresses) — **fall back to Nominatim** (OpenStreetMap), same retry policy, with a custom `User-Agent` header per its usage policy and `countrycodes=us` to respect the US-only scope (BR-6)
5. Map the Nominatim fallback response (only reached when Census returned 0 matches). **(Corrected 2026-10-06, second live-testing finding)**: the Nominatim query requests `limit=1` — trusting Nominatim's own relevance ranking (which factors in place importance/population) rather than fetching multiple candidates. This was changed after live testing showed Nominatim returning 5 different same-state Maryland localities all named "Germantown" for the query "Germantown MD" — treating those as a user-facing ambiguous prompt broke the project's own headline example. With `limit=1`:
   - **0 matches** → `GeocodeOutcome.NotFound`
   - **Exactly 1 match** (the normal case, since at most 1 is requested) → `GeocodeOutcome.Resolved`
   - **>1 matches** → `GeocodeOutcome.Ambiguous` (defensive handling only — not expected in normal operation given `limit=1`, kept in case the provider's behavior changes)
6. Cache the final outcome (including `NotFound`/`Ambiguous` outcomes — avoids repeatedly hitting either external API for a request that will keep failing/ambiguating in a short window) under the normalized key, with the configured TTL.
7. Return the `GeocodeOutcome` to the caller (`ParameterExtractor`).

### Consumers (current)
- **Strike intent** (`ExtractStrikeParameters` → `TryExtractLocation`)
- **Weather intent** (`ExtractWeatherParameters` → `TryExtractLocation`)

Both currently call the same private `TryExtractLocation` method in `ParameterExtractor.cs` — this method is refactored to call `LocationGeocoder` instead of the hardcoded dictionary, so both intents get the new behavior automatically with a single change. The coordinate-pattern (`lat/long` explicit) short-circuit in `TryExtractLocation` is preserved as-is (no geocoding call needed when the user already supplied raw coordinates).

### Radius Handling (extends existing `TryExtractRadius`)
- Unit/detection logic (km vs miles from phrasing) is unchanged.
- New rule: after extracting `(radius, radiusUnit)`, convert to miles for cap comparison (1 km ≈ 0.621371 miles) and reject with a validation error if the value exceeds 100 miles — **always**, whether the radius was explicit or the method's own default (Q4 answer A: no distinction; in practice the existing default of 50 km / ~31 mi is always within the cap, so this only ever triggers for explicit over-cap values, but the rule itself makes no exception based on explicit-vs-default).

### Error Surfacing
All three new error conditions (`Ambiguous`, `NotFound`, over-cap radius) produce a single descriptive string, consistent with the existing `{ "error": "<string>" }` convention already used throughout `ParameterExtractor`/`QueryController` (Q2 answer A — no new structured field).
