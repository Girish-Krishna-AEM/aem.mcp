# Requirements Clarification Questions — Informer Alerting/Search Service

Please answer each question by filling in the letter choice after the `[Answer]:` tag. Choose "Other" and describe if none fit.

## Question 1
Which real external endpoints will be used, and should the system **permanently** support falling back to the local stub JSON files (`data/informer/informer_list.json`, `data/informer/salesforce_oas_....json`) when the real endpoint is unavailable (e.g. off business hours) — similar to how the weather forecast module already has a fallback pattern — or is the stub data only a **temporary dev-time substitute** that should be removed once the real endpoints are confirmed working?

A) Permanent fallback — if the real API call fails/times out, automatically fall back to reading the local stub JSON file

B) Temporary only — wire up real HTTP calls now; stub files are just for local testing via a config flag/URL override, with no automatic fallback-on-failure logic in production code

C) Config-switch only — a single env var (e.g. `INFORMER_DATA_SOURCE=stub|live`) explicitly selects stub-file mode vs. live-API mode; no automatic failover between them

D) Other (please describe after [Answer]: tag below)

[Answer]:C

## Question 2
`informer_list.json` has fields `custName`, `informName`, `macAddress`, `informId`. When the user searches (e.g. "status of the informer Lakeside"), should matching be:

A) Exact match only (case-insensitive) on whichever field the value matches

B) Partial/substring match (case-insensitive) on `custName`/`informName`, exact match on `macAddress`/`informId`

C) Partial/substring + fuzzy match on all text fields (most forgiving, more false positives)

D) Other (please describe after [Answer]: tag below)

[Answer]:C

## Question 3
The stub list can contain multiple informers for the same customer (e.g. "AACS Lakeside High School Campus" has several). If a search matches **more than one** informer, what should happen?

A) Ask the user to disambiguate (list the matches with enough detail to pick one — similar to the existing location-geocoding ambiguity flow), then only query alerts for the chosen one

B) Automatically query and return alerts for **all** matched informers, grouped by informer in the response

C) Automatically pick the single best/first match (e.g. first non-disabled one) without asking, and mention in the response that other matches existed

D) Other (please describe after [Answer]: tag below)

[Answer]:D provide way to pick the selection of available informers; since Question is fuzzy search

## Question 4
There is an existing MCP tool `get_informer_status` (`unit-1/.../InformerStatusModule.cs`) that currently returns fully randomized fake data (status/power/zone/device type) — unrelated in shape to this new alert-history requirement. How should this new feature relate to it?

A) Replace it — remove the random-stub implementation and repurpose the `get_informer_status` tool name/contract for this new real search+alerts behavior

B) Leave it as-is — add a brand-new, separately-named tool (e.g. `get_informer_alerts`) for this feature, and leave `get_informer_status` untouched for now

C) Remove it entirely — it serves no purpose and isn't part of any current requirement; add only the new tool

D) Other (please describe after [Answer]: tag below)

[Answer]:A

## Question 5
The alerts stub response nests alerts inside multiple "sites"/geofences (`r[]`, e.g. `"nm": "old Outer Radius"`) with an `a[]` array of alerts under each — this is per the **user account** (`pasUserId`), not filtered to one specific device. Should the response:

A) Flatten all sites' alerts together into one combined, de-duplicated, time-sorted list (per your request to grab the result by `pasUserId` as a whole)

B) Keep them grouped by site name (`nm`) in the response, since different sites may mean different physical zones

C) Other (please describe after [Answer]: tag below)

[Answer]:D Since Informer status has changed over the period time - Maybe B would be good option

## Question 6
You said to return "all the utc dates; message; atn for now" per alert, to keep the response efficient. The stub shows these UTC timestamp fields per alert: `actutc` (action/created), `issutc` (issued), `exputc` (expires), `acutc` (auto-clear), `lmutc` (last modified). Confirm which UTC fields to keep:

A) All five (`actutc`, `issutc`, `exputc`, `acutc`, `lmutc`) + `atn` + `m`

B) Just the two most meaningful ones — `issutc` (when issued) and `exputc` (when it expires) — + `atn` + `m`

C) Other (please describe after [Answer]: tag below)

[Answer]:A

## Question 7
Your sample prompt ("last 1 day", "yesterday", "last 10 days", explicit start/end, all in local time → convert to UTC epoch) matches natural-language time parsing that already exists in Unit 2's `ParameterExtractor`/`TimeZoneInfo` conversion (built for the lightning-strike time-window queries). Should this new feature:

A) Reuse/extend that existing time-parsing logic in `ParameterExtractor` (consistent UX, less new code)

B) Build separate, independent time-parsing logic specific to informer alert queries

C) Other (please describe after [Answer]: tag below)

[Answer]:A

## Question 8
Should disabled informers (`isDisabled: true` in the stub data) be included in search results at all?

A) Exclude disabled informers from search matches entirely

B) Include them, but clearly flag them as disabled in the response

C) Other (please describe after [Answer]: tag below)

[Answer]:C if the prompt asked for disabled; Response can be it is disabled and not operateable

## Question 9
If a search finds **zero** matching informers, or the matched informer has **zero** alerts in the requested time range, what should the response say?

A) A short, plain-language "no informer found matching X" / "no alerts found for X between Y and Z" message — no error codes or raw payloads

B) Same as A, plus suggest close/similar matches if any exist (e.g. "did you mean 'AACS Lakeside High School Campus'?")

C) Other (please describe after [Answer]: tag below)

[Answer]: B
