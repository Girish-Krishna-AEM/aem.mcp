using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningCommon;

namespace LightningMcpServer.Tests.DomainModules;

public class SensorDiagnosticsModuleTests
{
    private readonly ISensorDiagnosticsModule _module;

    public SensorDiagnosticsModuleTests()
    {
        var loggerMock = new Mock<ILogger<SensorDiagnosticsModule>>();
        _module = new SensorDiagnosticsModule(loggerMock.Object);
    }

    [Fact]
    public void GetSensorDiagnostics_WithValidId_ReturnsDiagnostics()
    {
        var request = new GetSensorDiagnosticsRequest { SensorId = "sensor-001" };

        var response = _module.GetSensorDiagnostics(request);

        Assert.NotNull(response);
        Assert.True(response.DetectionEfficiency >= 85 && response.DetectionEfficiency <= 100);
        Assert.NotEmpty(response.Status);
    }

    [Fact]
    public void GetSensorDiagnostics_WithEmptyId_ThrowsArgumentException()
    {
        var request = new GetSensorDiagnosticsRequest { SensorId = "" };

        Assert.Throws<ArgumentException>(() => _module.GetSensorDiagnostics(request));
    }

    [Fact]
    public void GetSensorDiagnostics_DeterministicResults()
    {
        var request1 = new GetSensorDiagnosticsRequest { SensorId = "sensor-001" };
        var request2 = new GetSensorDiagnosticsRequest { SensorId = "sensor-001" };

        var response1 = _module.GetSensorDiagnostics(request1);
        var response2 = _module.GetSensorDiagnostics(request2);

        Assert.Equal(response1.DetectionEfficiency, response2.DetectionEfficiency);
        Assert.Equal(response1.Status, response2.Status);
    }
}
