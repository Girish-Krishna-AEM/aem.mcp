using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface IInformerStatusModule
{
    InformerStatus GetInformerStatus(GetInformerStatusRequest request);
}
