using LightningMcpServer.ExternalApis;

namespace LightningMcpServer.Tests.Integration;

/// <summary>Deterministic stand-in for the real HTTP-backed client, used so integration
/// tests exercise MCP routing/wiring without requiring network access or a live API key.</summary>
public class FakeLightningPulseApiClient : ILightningPulseApiClient
{
    public Task<PulsesApiResponse> GetPulsesAsync(
        DateTime? startDateTime,
        DateTime? endDateTime,
        int? pulseType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PulsesApiResponse
        {
            Code = 200,
            Pulses = new List<PulseDto>
            {
                new() { Lat = 30.2672, Lon = -97.7431, Cur = 8.146, Typ = 0, Ts = DateTime.UtcNow }
            }
        });
    }
}
