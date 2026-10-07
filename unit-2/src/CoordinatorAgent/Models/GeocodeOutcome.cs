namespace CoordinatorAgent.Models;

public class GeocodeResult
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string MatchedAddress { get; init; } = string.Empty;
}

public enum GeocodeOutcomeKind
{
    Resolved,
    Ambiguous,
    NotFound
}

public class GeocodeOutcome
{
    public GeocodeOutcomeKind Kind { get; }
    public GeocodeResult? Result { get; }
    public IReadOnlyList<string> Candidates { get; }

    private GeocodeOutcome(GeocodeOutcomeKind kind, GeocodeResult? result, IReadOnlyList<string> candidates)
    {
        Kind = kind;
        Result = result;
        Candidates = candidates;
    }

    public static GeocodeOutcome Resolved(GeocodeResult result) =>
        new(GeocodeOutcomeKind.Resolved, result, Array.Empty<string>());

    public static GeocodeOutcome Ambiguous(IReadOnlyList<string> candidates) =>
        new(GeocodeOutcomeKind.Ambiguous, null, candidates);

    public static readonly GeocodeOutcome NotFound =
        new(GeocodeOutcomeKind.NotFound, null, Array.Empty<string>());
}
