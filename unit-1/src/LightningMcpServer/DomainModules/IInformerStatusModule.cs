using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface IInformerStatusModule
{
    InformerStatusResult GetInformerStatus(GetInformerStatusRequest request);
}
