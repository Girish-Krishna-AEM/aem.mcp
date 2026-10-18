using LightningCommon;

namespace LightningMcpServer.ExternalApis;

public interface IInformerApiClient
{
    Task<IReadOnlyList<InformerRecord>> GetInformersAsync(CancellationToken cancellationToken = default);
}
