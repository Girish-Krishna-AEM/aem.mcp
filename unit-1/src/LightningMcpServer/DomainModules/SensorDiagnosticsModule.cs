using LightningCommon;
using System;

namespace LightningMcpServer.DomainModules;

public class SensorDiagnosticsModule : ISensorDiagnosticsModule
{
    private readonly ILogger<SensorDiagnosticsModule> _logger;

    public SensorDiagnosticsModule(ILogger<SensorDiagnosticsModule> logger)
    {
        _logger = logger;
    }

    public SensorDiagnostics GetSensorDiagnostics(GetSensorDiagnosticsRequest request)
    {
        _logger.LogInformation("Sensor diagnostics requested: sensor_id={SensorId}", request.SensorId);

        ValidateRequest(request);

        var seed = GenerateSeed(request.SensorId);
        var random = new Random(seed);

        var diagnostics = new SensorDiagnostics
        {
            DetectionEfficiency = 85 + random.NextDouble() * 15,
            GpsVisibility = 90 + random.NextDouble() * 10,
            TrackedSatellites = 12 + random.Next(0, 9),
            NoiseLevel = 0.5 + random.NextDouble() * 1.5,
            Uptime = 95 + random.NextDouble() * 5,
            LastCalibration = DateTime.UtcNow.AddDays(-random.Next(0, 30)).ToString("yyyy-MM-dd"),
            Snr = 15 + random.NextDouble() * 20,
            Status = random.NextDouble() > 0.1 ? "healthy" : "degraded"
        };

        _logger.LogInformation(
            "Sensor diagnostics result: sensor_id={SensorId}, status={Status}, uptime={Uptime:F1}%",
            request.SensorId, diagnostics.Status, diagnostics.Uptime);

        return diagnostics;
    }

    private void ValidateRequest(GetSensorDiagnosticsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SensorId))
            throw new ArgumentException("SensorId must not be empty");
    }

    private int GenerateSeed(string sensorId) => sensorId.GetHashCode();
}
