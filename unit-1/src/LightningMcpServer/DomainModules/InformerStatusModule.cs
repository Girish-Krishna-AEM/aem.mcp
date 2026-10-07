using LightningCommon;
using System;

namespace LightningMcpServer.DomainModules;

public class InformerStatusModule : IInformerStatusModule
{
    private readonly ILogger<InformerStatusModule> _logger;
    private readonly string[] _deviceTypes = { "Strobe", "Horn", "Siren", "Hybrid" };

    public InformerStatusModule(ILogger<InformerStatusModule> logger)
    {
        _logger = logger;
    }

    public InformerStatus GetInformerStatus(GetInformerStatusRequest request)
    {
        _logger.LogInformation(
            "Informer status requested: informer_id={InformerId}, zone={Zone}",
            request.InformerId, request.Zone);

        ValidateRequest(request);

        var seed = GenerateSeed(request.InformerId ?? request.Zone ?? "unknown");
        var random = new Random(seed);

        var status = new InformerStatus
        {
            Status = random.NextDouble() > 0.05 ? "active" : "inactive",
            LastActivation = DateTime.UtcNow.AddMinutes(-random.Next(1, 1440)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            PowerStatus = random.NextDouble() > 0.1 ? "normal" : "low",
            Zone = request.Zone ?? $"Zone-{Math.Abs(request.InformerId?.GetHashCode() ?? 0) % 10}",
            DeviceType = _deviceTypes[random.Next(_deviceTypes.Length)]
        };

        _logger.LogInformation(
            "Informer status result: status={Status}, device={DeviceType}, power={PowerStatus}",
            status.Status, status.DeviceType, status.PowerStatus);

        return status;
    }

    private void ValidateRequest(GetInformerStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InformerId) && string.IsNullOrWhiteSpace(request.Zone))
            throw new ArgumentException("Either InformerId or Zone must be provided");
    }

    private int GenerateSeed(string identifier) => identifier.GetHashCode();
}
