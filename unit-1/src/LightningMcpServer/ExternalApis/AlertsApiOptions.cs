namespace LightningMcpServer.ExternalApis;

public class AlertsApiOptions
{
    public string BaseUrl { get; set; } = "http://qa.alerts.api.enqa.co";
    public int TimeoutSeconds { get; set; } = 10;
    public string DataSource { get; set; } = "stub";
    public string StubFilePath { get; set; } = "data/informer/salesforce_oas_0014000000gGR2bAAG.2050.json";
}
