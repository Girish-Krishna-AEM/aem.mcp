# NFR Requirements Plan — Unit 2: Coordinator Agent (FR-4 Addendum: Location Geocoding Utility)

## Plan Steps

- [ ] Determine HTTP timeout value for Census Geocoder calls
- [ ] Determine retry/backoff concrete strategy (delay between the up-to-3 attempts)
- [ ] Determine cache TTL and cache size-bound approach
- [ ] Determine HttpClient lifecycle/management approach (consistent with existing patterns in the codebase, if any)
- [ ] Determine logging level of detail for geocoding calls (cache hit/miss, retries, outcome)
- [ ] Determine testing approach for the new external dependency (mocked HTTP vs real network calls in tests)

## Clarifying Questions

### Question 1
What HTTP timeout should apply to each individual call to the Census Geocoder?

A) 5 seconds (matches the "short timeout" language from the original FR-4 requirement)

B) 10 seconds (a bit more tolerant, since Census Geocoder has no documented SLA)

C) Other (please describe after [Answer]: tag below)

[Answer]: 10

### Question 2
What backoff delay should apply between retry attempts (up to 2 retries, 3 attempts total, per FR-4)?

A) Fixed delay — e.g. 500ms between each attempt, simplest to implement and reason about

B) Exponential backoff — e.g. 500ms, then 1000ms, doubling each retry

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 3
What TTL should geocoded results (including ambiguous/not-found outcomes) be cached for, and should the cache be unbounded or size-capped?

A) 1 hour TTL, unbounded size (acceptable for a POC — process restarts clear it anyway, traffic volume is low)

B) 1 hour TTL, capped at a fixed max entry count (e.g. 1000) with oldest-first eviction, to bound memory use

C) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 4
How should the `HttpClient` used to call the Census Geocoder be managed?

A) Use `IHttpClientFactory` with a named/typed client registered in DI (`Program.cs`) — standard ASP.NET Core practice, avoids socket exhaustion

B) Other (please describe after [Answer]: tag below — e.g. if there's an existing HttpClient pattern elsewhere in CoordinatorAgent you want this to match)

[Answer]: A

### Question 5
How should the new tests for `LocationGeocoder` be structured, consistent with this project's existing testing approach?

A) Pure unit tests with a mocked `HttpMessageHandler`/`HttpClient` (no real network calls) for all paths: single match, ambiguous, not-found, timeout/retry, cache hit — matching the existing `CoordinatorAgent.Tests` pattern of mocking dependencies

B) Mocked unit tests PLUS one opt-in live-integration test that hits the real Census Geocoder API (skipped by default/CI, run manually) to catch real API contract drift

C) Other (please describe after [Answer]: tag below)

[Answer]: A
