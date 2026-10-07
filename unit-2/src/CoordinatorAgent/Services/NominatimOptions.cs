namespace CoordinatorAgent.Services;

public class NominatimOptions
{
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    public int TimeoutSeconds { get; set; } = 10;
    public string UserAgent { get; set; } = "LightningDetectionCoordinatorAgent/1.0 (POC)";
}
