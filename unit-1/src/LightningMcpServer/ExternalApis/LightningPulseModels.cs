using System.Text.Json.Serialization;

namespace LightningMcpServer.ExternalApis;

public class PulsesApiResponse
{
    [JsonPropertyName("responseId")]
    public string? ResponseId { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("error")]
    public PulseApiError? Error { get; set; }

    [JsonPropertyName("pulses")]
    public List<PulseDto> Pulses { get; set; } = new();
}

public class PulseApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class PulseDto
{
    [JsonPropertyName("cur")]
    public double Cur { get; set; }

    [JsonPropertyName("pol")]
    public string? Pol { get; set; }

    [JsonPropertyName("ts")]
    public DateTime Ts { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("typ")]
    public int Typ { get; set; }

    [JsonPropertyName("eea")]
    public double Eea { get; set; }

    [JsonPropertyName("eei")]
    public double Eei { get; set; }

    [JsonPropertyName("eeb")]
    public int Eeb { get; set; }
}
