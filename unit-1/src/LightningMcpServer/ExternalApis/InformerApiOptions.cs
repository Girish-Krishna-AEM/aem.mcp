namespace LightningMcpServer.ExternalApis;

public class InformerApiOptions
{
    public string BaseUrl { get; set; } = "http://qa.oas.api.enqa.co";
    public int TimeoutSeconds { get; set; } = 10;
    public string DataSource { get; set; } = "stub";
    public string StubFilePath { get; set; } = "data/informer/informer_list.json";
}
