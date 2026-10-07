# Requirements Clarification Questions — Location Geocoding Utility

New requirement: accept a free-text location (City/State, ZIP code, or street address) from the user's natural-language query (e.g., "Get all the LX for Germantown MD around 50 miles"), convert it to lat/lon internally via a plain utility (not an MCP tool), and feed that into the existing lat/lon/radius strike-query flow.

Please answer each question by filling in the letter choice after the `[Answer]:` tag.

## Question 1
Which free geocoding provider should the utility use? ("Free, open source, no limitation" needs a concrete choice — each has different tradeoffs below.)

A) OpenStreetMap Nominatim — free, no API key, global coverage, but has a strict usage policy (max ~1 request/sec, requires a custom User-Agent, not for heavy production traffic)

B) US Census Bureau Geocoder — free, no API key, no rate limit documented, but **US addresses/ZIPs only** (no global coverage)

C) Both: try US Census Geocoder first (fast, unlimited, US-only), fall back to Nominatim for non-US or when Census has no match

D) Other (please describe after [Answer]: tag below — e.g. you already have an API key for a different provider)

[Answer]: B

## Question 2
Where should this geocoding utility live in the codebase?

A) New internal utility class in `LightningCommon` (unit-1's shared library) — reusable by both Coordinator and MCP Server

B) New internal utility class inside Unit 2 (`CoordinatorAgent`) only — since that's where natural-language queries are parsed today

C) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
Which location input formats must be supported?

A) City + State only (e.g., "Germantown MD")

B) City + State, and ZIP code (e.g., "20874")

C) City + State, ZIP code, and full street address (e.g., "123 Main St, Germantown, MD 20874")

D) Other (please describe after [Answer]: tag below)

[Answer]: C

## Question 4
What should happen when the location text is ambiguous (matches multiple places) or not found at all?

A) Ambiguous → use the first/best match automatically; Not found → return a clear 400 error to the user

B) Ambiguous → return a 400 error listing the possible matches for the user to disambiguate; Not found → return a clear 400 error

C) Other (please describe after [Answer]: tag below)

[Answer]: B

## Question 5
Should geocoding results be cached to avoid repeated external lookups for the same location string?

A) Yes — simple in-memory cache (e.g., dictionary with TTL), cleared on restart, no new infrastructure

B) No — always call the geocoding API fresh (simplest, matches "no limitation" assumption)

C) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6
Default radius unit and limits when the user doesn't specify, or specifies only a number (e.g., "50 miles")?

A) Miles only, with a max cap (e.g., 100 miles) to protect the Lightning Pulse API from oversized queries

B) Support both miles and kilometers (detect from phrasing like "50 miles" vs "80 km"), with a max cap

C) Other (please describe after [Answer]: tag below)

[Answer]: B with Max 100 mile max

## Question 7
Geographic scope — does this need to support locations outside the United States?

A) US-only for now (matches current project scope / Phase 1 POC)

B) Global (any country)

C) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 8
How should the utility handle geocoding API timeouts or transient failures?

A) Single attempt with a short timeout (e.g., 5s); on failure, return a clear error to the user — no retries (matches current POC simplicity, no Resiliency extension enabled)

B) Retry once or twice with backoff before failing

C) Other (please describe after [Answer]: tag below)

[Answer]: B
