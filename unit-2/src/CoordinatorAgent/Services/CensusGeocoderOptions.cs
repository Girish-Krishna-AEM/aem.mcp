namespace CoordinatorAgent.Services;

public class CensusGeocoderOptions
{
    public string BaseUrl { get; set; } = "https://geocoding.geo.census.gov/geocoder/";
    public int TimeoutSeconds { get; set; } = 10;
}
