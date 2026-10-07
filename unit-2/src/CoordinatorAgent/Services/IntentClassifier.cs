using System.Text.RegularExpressions;
using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public class IntentClassifier : IIntentClassifier
{
    private static readonly (Intent Intent, Regex Pattern)[] Patterns =
    {
        (Intent.Strike, new Regex(@"\b(strike|lightning|lx|near|location)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
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
