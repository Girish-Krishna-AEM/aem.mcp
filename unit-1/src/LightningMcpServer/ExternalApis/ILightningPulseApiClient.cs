namespace LightningMcpServer.ExternalApis;

public interface ILightningPulseApiClient
{
    /// <summary>
    /// Fetches lightning pulses from the external pulse API.
    /// </summary>
    /// <param name="startDateTime">Defaults to <paramref name="endDateTime"/> minus 5 minutes when null. Must be chronologically before endDateTime (API requirement).</param>
    /// <param name="endDateTime">Defaults to current UTC time when null.</param>
    /// <param name="pulseType">0 = Cloud-to-Ground (CG), 1 = Intra-Cloud (IC). Omitted from the request when null.</param>
    Task<PulsesApiResponse> GetPulsesAsync(
        DateTime? startDateTime,
        DateTime? endDateTime,
        int? pulseType,
        CancellationToken cancellationToken = default);
}
