using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using LightningMcpServer.DomainModules;
using LightningCommon;

namespace LightningMcpServer.Tests.DomainModules;

public class InformerStatusModuleTests
{
    private readonly IInformerStatusModule _module;

    public InformerStatusModuleTests()
    {
        var loggerMock = new Mock<ILogger<InformerStatusModule>>();
        _module = new InformerStatusModule(loggerMock.Object);
    }

    [Fact]
    public void GetInformerStatus_WithInformerId_ReturnsStatus()
    {
        var request = new GetInformerStatusRequest { InformerId = "informer-001" };

        var response = _module.GetInformerStatus(request);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Status);
        Assert.NotEmpty(response.DeviceType);
    }

    [Fact]
    public void GetInformerStatus_WithZone_ReturnsStatus()
    {
        var request = new GetInformerStatusRequest { Zone = "zone-a" };

        var response = _module.GetInformerStatus(request);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Status);
    }

    [Fact]
    public void GetInformerStatus_WithBoth_ReturnsStatus()
    {
        var request = new GetInformerStatusRequest
        {
            InformerId = "informer-001",
            Zone = "zone-a"
        };

        var response = _module.GetInformerStatus(request);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Status);
    }

    [Fact]
    public void GetInformerStatus_WithNeither_ThrowsArgumentException()
    {
        var request = new GetInformerStatusRequest();

        Assert.Throws<ArgumentException>(() => _module.GetInformerStatus(request));
    }
}
