using LightningCommon;

namespace LightningMcpServer.DomainModules;

public interface ISensorDiagnosticsModule
{
    SensorDiagnostics GetSensorDiagnostics(GetSensorDiagnosticsRequest request);
}
