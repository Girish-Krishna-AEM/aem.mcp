namespace LightningMcpServer.ExternalApis;

public interface ILightningPulseApiClient
{
    /// <summary>
    /// Fetches lightning pulses from the external pulse API, filtered server-side to a
    /// point + radius (the API's "p"/"radius" query parameters) rather than pulling an
    /// unfiltered global set and filtering client-side.
    /// </summary>
    /// <param name="startDateTime">Defaults to <paramref name="endDateTime"/> minus 5 minutes when null. Must be chronologically before endDateTime (API requirement).</param>
    /// <param name="endDateTime">Defaults to current UTC time when null.</param>
    /// <param name="pulseType">0 = Cloud-to-Ground (CG), 1 = Intra-Cloud (IC). Omitted from the request when null.</param>
    /// <param name="latitude">Center point latitude for the "p" parameter (point, format "lat,lon" per the API).</param>
    /// <param name="longitude">Center point longitude for the "p" parameter.</param>
    /// <param name="radius">Search radius value; sent as "radius=&lt;value&gt;&lt;unit&gt;" (e.g. "50mi") — the API rejects a bare number with no unit.</param>
    /// <param name="radiusUnit">"miles" or "km" — mapped to the API's "mi"/"km" unit suffix.</param>
    Task<PulsesApiResponse> GetPulsesAsync(
        DateTime? startDateTime,
        DateTime? endDateTime,
        int? pulseType,
        double latitude,
        double longitude,
        double radius,
        string radiusUnit,
        CancellationToken cancellationToken = default);
}
