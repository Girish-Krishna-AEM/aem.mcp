using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface IStrikeDetectionModule
{
    GetLightningStrikesNearLocationResponse GetLightningStrikesNearLocation(
        GetLightningStrikesNearLocationRequest request);
}
