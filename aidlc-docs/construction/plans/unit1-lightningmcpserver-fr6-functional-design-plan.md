# Functional Design Plan — Unit 1 (`LightningMcpServer`), FR-6 Informer Search & Alerting Service

## Plan Steps

- [ ] Define domain entities (informer record, search match, disambiguation candidate, alert record, alert site group, overall result envelope)
- [ ] Define search business rules (fuzzy match across 4 fields, disabled-informer handling, disambiguation, no-match + close-match suggestion)
- [ ] Define alerts-lookup business rules (site grouping, field trimming, no-alerts messaging, stub/live switch, 1-hour cache-aside)
- [ ] Define the replaced `get_informer_status` MCP tool's input/output contract
- [ ] Generate `business-logic-model.md`, `business-rules.md`, `domain-entities.md` for Unit 1

## Clarifying Questions

### Question 1
Your sample prompts include "Give the status of the informer `<customerName>` between `<start>` and `<end>` **or last 1 day**" — all of which specify *some* time phrase. If a user asks about an informer with **no time phrase at all** (e.g. just "status of the informer Lakeside"), what should the default time range be?

A) Default to the last 24 hours (last 1 day)

B) Default to the last 1 hour

C) Require a time phrase — if none is given, ask the user to specify one rather than guessing a default

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 2
For the "no informer found — did you mean X?" close-match suggestion (your Q9 answer), how should "close" be determined? The search matching itself (Q2 answer) is already substring/fuzzy on all 4 fields, so a true zero-match case means even substring matching found nothing.

A) No separate "close match" algorithm needed — since matching is already fuzzy/substring-based, a genuine zero-result case is rare; just report "no informer found" without suggestions (simplifies Q9 to effectively the same as not suggesting)

B) Add a simple approximate-string-distance check (e.g. Levenshtein distance within a small threshold) against `custName`/`informName` specifically for the suggestion message, separate from the main fuzzy search

C) Other (please describe after [Answer]: tag below)

[Answer]:A

