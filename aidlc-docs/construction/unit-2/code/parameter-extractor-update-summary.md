# ParameterExtractor Update — Code Summary (FR-4 Addendum)

## Files Modified

| File | Change |
|---|---|
| `unit-2/src/CoordinatorAgent/Services/IParameterExtractor.cs` | `ExtractParameters` return type changed to `Task<(bool, JsonElement?, string?)>` |
| `unit-2/src/CoordinatorAgent/Services/ParameterExtractor.cs` | See below |
| `unit-2/src/CoordinatorAgent/Controllers/QueryController.cs` | One-line change: `await _parameterExtractor.ExtractParameters(...)` |

## `ParameterExtractor.cs` Changes
- **Deleted**: the hardcoded `KnownLocations` 10-city dictionary.
- **Added**: `ILocationGeocoder` constructor dependency.
- **Added**: `LocationPhrasePattern` regex — extracts just the location substring (e.g. "Germantown MD" out of "...for Germantown MD around 50 miles") from the query text, so only the relevant text is sent to the geocoder. Stops at radius/time-window qualifier words (`within`, `over`, `around`, `in the last`, `in the past`, a bare radius number+unit) or end-of-string/`?`.
- **Changed**: `TryExtractLocation` is now `async`, returns `(bool Found, double Latitude, double Longitude, string? Error)`. Explicit `lat`/`long` text still short-circuits with no geocoding call (unchanged behavior). Otherwise, extracts the location phrase and calls `ILocationGeocoder.GeocodeAsync`, branching on the returned `GeocodeOutcome`:
  - `Resolved` → returns the coordinates
  - `Ambiguous` → returns an error listing the candidate matches
  - `NotFound` → returns a clear not-found error
- **Changed**: `ExtractStrikeParameters` and `ExtractWeatherParameters` are now `async`, both calling the shared `TryExtractLocation` — so **both intents** benefit from real geocoding (per the user's explicit instruction that this should be a general-purpose capability, not Strike-only).
- **Added**: 100-mile radius cap check in `ExtractStrikeParameters`, converting km→miles for comparison, rejecting with a validation error if exceeded.
- **Changed**: `ExtractParameters` is now `async`, awaiting the Strike/Weather branches (Sensor/Informer extraction remains synchronous internally, wrapped to match the async interface).
