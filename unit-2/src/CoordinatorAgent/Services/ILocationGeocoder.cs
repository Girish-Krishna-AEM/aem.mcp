using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public interface ILocationGeocoder
{
    Task<GeocodeOutcome> GeocodeAsync(string locationText, CancellationToken cancellationToken = default);
}
