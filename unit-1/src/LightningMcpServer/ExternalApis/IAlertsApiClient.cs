using LightningCommon;

namespace LightningMcpServer.ExternalApis;

public interface IAlertsApiClient
{
    Task<IReadOnlyList<AlertSiteGroup>> GetAlertsAsync(
        string pasUserId,
        long startDateTimeUtc,
        long endDateTimeUtc,
        CancellationToken cancellationToken = default);
}
