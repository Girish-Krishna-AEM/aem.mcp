using System.Text.RegularExpressions;
using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public class IntentClassifier : IIntentClassifier
{
    private static readonly (Intent Intent, Regex Pattern)[] Patterns =
    {
        // "near"/"location" were deliberately removed from this pattern (2026-10-08): they're
        // generic locational phrasing, not Strike-specific, and since Strike was checked first,
        // ANY query mentioning "near <place>" — including weather queries like "forecast near
        // Urbana, MD" — was being misrouted here before Weather's pattern was ever tried.
        (Intent.Strike, new Regex(@"\b(strike|lightning|lx)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (Intent.Weather, new Regex(@"\b(forecast|weather|temperature|rain|precipitation)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (Intent.Sensor, new Regex(@"\b(sensor|diagnostic|efficiency|calibration)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (Intent.Informer, new Regex(@"\b(informer|device|status|power|horn|strobe)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    };

    private readonly ILogger<IntentClassifier> _logger;

    public IntentClassifier(ILogger<IntentClassifier> logger)
    {
        _logger = logger;
    }

    public Intent? Classify(string query)
    {
        foreach (var (intent, pattern) in Patterns)
        {
            if (pattern.IsMatch(query))
            {
                _logger.LogInformation("Intent classified: {Intent}", intent);
                return intent;
            }
        }

        _logger.LogWarning("Unable to classify intent for query");
        return null;
    }
}
