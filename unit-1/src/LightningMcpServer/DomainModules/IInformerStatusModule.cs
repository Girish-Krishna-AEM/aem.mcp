using System.Threading.Tasks;
using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface IInformerStatusModule
{
    InformerStatusResult GetInformerStatus(GetInformerStatusRequest request);

    Task<GetInformersNearLocationResponse> GetInformersNearLocationAsync(GetInformersNearLocationRequest request);
}
