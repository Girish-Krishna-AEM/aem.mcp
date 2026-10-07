# Functional Design Plan — Unit 2: Coordinator Agent (FR-4 Addendum: Location Geocoding Utility)

## Scope
Design the business logic for resolving free-text location input (City+State, ZIP, or full street address) to lat/lon inside `CoordinatorAgent`, replacing the hardcoded `KnownLocations` dictionary in `ParameterExtractor.TryExtractLocation`. Unit 1 is unaffected (out of scope here).

## Plan Steps

- [ ] Define `GeocodeResult` domain model (resolved lat/lon + normalized/matched address string)
- [ ] Define cache-entry model (key normalization strategy, value, TTL expiry)
- [ ] Define business rules for: single match, ambiguous match (multiple candidates), no match, non-US input
- [ ] Define the US Census Geocoder request/response mapping (which endpoint, which match fields map to `GeocodeResult`)
- [ ] Define radius-unit detection + 100-mile cap business rule (extends existing `TryExtractRadius`)
- [ ] Define error-message contract for ambiguous/not-found/over-cap cases (consistent with existing `{ "error": "<string>" }` convention used elsewhere in `QueryController`)
- [ ] Define retry/backoff behavior at the business-logic level (handed to NFR Design for concrete values)
- [ ] Produce `business-logic-model.md`, `business-rules.md`, `domain-entities.md`

## Clarifying Questions

Please fill in the `[Answer]:` tags below.

### Question 1
The US Census Geocoder has two relevant endpoints: `/locations/onelineaddress` (accepts any single free-text string — City+State, ZIP, or full address, in one field) and `/locations/address` (requires separately-structured street/city/state/zip fields). Which should the utility use?

A) `/locations/onelineaddress` only — pass the user's location text through as-is in one field; simplest, handles all 3 input formats (City+State, ZIP, full address) uniformly

B) Try to detect the input format first (ZIP-only regex, "City, State" pattern, full address) and route to the structured `/locations/address` endpoint when enough fields can be parsed out, falling back to `/locations/onelineaddress` otherwise

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2
When the Census Geocoder returns multiple candidate matches (ambiguous), how should the error message be worded/structured, given the existing error convention in this codebase is a single `{ "error": "<string>" }` field (see `QueryController.cs`, `ParameterExtractor.cs`)?

A) Keep the existing simple string-error convention — embed the candidate list directly in the string message, e.g. `"Multiple locations match 'Springfield'. Did you mean: Springfield, IL; Springfield, MO; Springfield, MA? Please specify state or ZIP."`

B) Extend the response shape with a new structured field (e.g. `{ "error": "...", "candidates": [...] }`) specifically for this ambiguous case — a one-off deviation from the existing single-string convention, for easier machine parsing by UI/API callers

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3
Cache key normalization — should the geocoding cache key be:

A) The raw location string, lowercased and trimmed only (e.g. "Germantown MD" and "germantown md " hit the same cache entry; "Germantown, MD" with a comma would NOT match "Germantown MD")

B) A more aggressive normalization — lowercase, trim, strip punctuation/commas, collapse whitespace (so "Germantown, MD" and "Germantown MD" hit the same cache entry)

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4
The existing `TryExtractRadius` already parses a numeric radius + unit (km/miles) from the query text, defaulting to `(50, "km")` when absent. For the new 100-mile cap: when the detected/default radius exceeds the cap, what should happen?

A) Reject with a validation error (as specified in FR-4) — no silent clamping, regardless of whether the radius was explicit or came from the default

B) Reject only if the user *explicitly* specified a radius exceeding the cap; if no radius was specified at all (using the 50km/~31mi default), never reject on cap grounds (the default is always within range anyway, but confirming no edge-case surprises)

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 5
Should the new `LocationGeocoder` apply only to the **Strike** intent (`get_lightning_strikes_near_location`), or also replace location resolution for the **Weather** intent (`get_weather_forecast`), which also calls `TryExtractLocation` today via the same hardcoded 10-city dictionary?

A) Apply to both Strike and Weather intents — both currently share the same `TryExtractLocation` method and hardcoded dictionary; replacing it benefits both symmetrically with no extra design cost

B) Apply to Strike intent only — Weather intent keeps using the existing hardcoded dictionary for now (narrower scope, matches the literal example in the original request which was about strike/lightning data)

C) Other (please describe after [Answer]: tag below)

[Answer]: C Should apply for Strike and Weather intents or anyother future API calls whereever you see city,state zipcode or full address this LocationGeoCoder should kickoff
