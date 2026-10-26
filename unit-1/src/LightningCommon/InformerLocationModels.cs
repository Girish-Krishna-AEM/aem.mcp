using System;
using System.Collections.Generic;

namespace LightningCommon;

public class GetInformersNearLocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; }
    public string RadiusUnit { get; set; } = "miles";
    public long? StartDateTimeUtc { get; set; }
    public long? EndDateTimeUtc { get; set; }
}

public class InformerLocationMatch
{
    public int InformId { get; set; }
    public string InformName { get; set; } = string.Empty;
    public string CustName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public double DistanceMiles { get; set; }
    public int AlertCount { get; set; }
    public int SiteCount { get; set; }
}

public class GetInformersNearLocationResponse
{
    public IReadOnlyList<InformerLocationMatch> Informers { get; set; } = Array.Empty<InformerLocationMatch>();
    public bool Truncated { get; set; }
    public int TotalMatchesInRadius { get; set; }
}
