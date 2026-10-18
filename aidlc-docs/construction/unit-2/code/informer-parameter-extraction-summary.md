# Informer Parameter Extraction — Code Summary (FR-6, Unit 2)

## Modified files
- `unit-2/src/CoordinatorAgent/Services/ParameterExtractor.cs`:
  - Removed `InformerIdPattern`/`ZonePattern` (strict `informer-XXX`/`zone-X` matching).
  - Added `InformerSearchPhrasePattern` — captures free text after "informer" up to a time-window qualifier or end of string, same style as `LocationPhrasePattern`.
  - Rewrote `ExtractInformerParameters`: extracts `search`; reuses the existing `TryExtractTimeWindow` for the optional time range, converting resolved UTC `DateTime`s to Unix epoch seconds for `startDateTimeUtc`/`endDateTimeUtc`; omits both when no time phrase is present (BR-12/BR-13).
- `unit-2/tests/CoordinatorAgent.Tests/Services/ParameterExtractorTests.cs` — replaced the old zone/informerId tests with search-phrase + time-window coverage.
