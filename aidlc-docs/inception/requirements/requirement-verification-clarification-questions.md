# Requirements Clarification Questions — Follow-up (Informer Alerting/Search Service)

Your Question 8 answer ("if the prompt asked for disabled; Response can be it is disabled and not operateable") could mean two different behaviors in the code. Please pick the one that matches your intent.

## Clarification Question 1
When a search matches an informer that has `isDisabled: true`, what should happen?

A) By default, disabled informers are **excluded from search matches entirely** (treated as if they don't exist) — UNLESS the user's query explicitly asks about disabled/inactive informers, in which case include the match but respond "this informer is disabled and not operational" instead of calling the alerts API

B) **Always include** disabled informers in search matches regardless of how the query is phrased, but whenever a matched informer turns out to be disabled, skip the alerts API call and respond "disabled, not operational" instead of alert history

C) Other (please describe after [Answer]: tag below)

[Answer]:B
