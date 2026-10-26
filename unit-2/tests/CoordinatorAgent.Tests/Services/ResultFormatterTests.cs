using System.Text.Json;
using CoordinatorAgent.Models;
using CoordinatorAgent.Services;
using Xunit;

namespace CoordinatorAgent.Tests.Services;

public class ResultFormatterTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ResultFormatter.Format returns an anonymous type (internal to the CoordinatorAgent
    // assembly), so dynamic/reflection access from this test assembly can't see it directly —
    // round-trip through JSON instead, which only relies on public property values.
    private static string Summary(object formatted) =>
        JsonSerializer.SerializeToElement(formatted).GetProperty("summary").GetString()!;

    [Fact]
    public void Format_StrikeIntentWithNoStrikes_ReturnsNoStrikesSummary()
    {
        var result = Parse("""{"strikeCount":0,"cloudToGroundCount":0,"intraCloudCount":0,"maxIntensity":0,"nearestStrikeDistance":0}""");

        var formatted = ResultFormatter.Format("get_lightning_strikes_near_location", Intent.Strike, result);

        Assert.Contains("No lightning strikes detected", Summary(formatted));
    }

    [Fact]
    public void Format_InformerFound_ReturnsSingleInformerSummary()
    {
        var result = Parse("""
            {"kind":"Found","match":{"informId":1,"informName":"Lakeside","custName":"AACS","macAddress":"00:00:00:00:00:01","isDisabled":false},
             "alertSiteGroups":[{"siteName":"Main","alerts":[{"atn":"LX","message":"m"}]}],"candidates":[]}
            """);

        var formatted = ResultFormatter.Format("get_informer_status", Intent.Informer, result);

        Assert.Equal("Informer 'Lakeside': 1 alert(s) across 1 site(s).", Summary(formatted));
    }

    [Fact]
    public void Format_InformerLocationList_ReturnsCountsOnlySummary()
    {
        var result = Parse("""
            {"informers":[
                {"informId":1,"informName":"A","custName":"Cust A","macAddress":"00:00:00:00:00:01","isDisabled":false,"distanceMiles":1.2,"alertCount":2,"siteCount":1},
                {"informId":2,"informName":"B","custName":"Cust B","macAddress":"00:00:00:00:00:02","isDisabled":true,"distanceMiles":3.4,"alertCount":0,"siteCount":0}
             ],"truncated":false,"totalMatchesInRadius":2}
            """);

        var formatted = ResultFormatter.Format("get_informers_near_location", Intent.Informer, result);

        Assert.Equal("Found 2 informer(s) within the requested radius: 2 alert(s) total across 1 site(s). (1 disabled.)", Summary(formatted));
    }

    [Fact]
    public void Format_InformerLocationListTruncated_IncludesTruncationNote()
    {
        var result = Parse("""
            {"informers":[{"informId":1,"informName":"A","custName":"Cust A","macAddress":"00:00:00:00:00:01","isDisabled":false,"distanceMiles":1.2,"alertCount":0,"siteCount":0}],
             "truncated":true,"totalMatchesInRadius":30}
            """);

        var formatted = ResultFormatter.Format("get_informers_near_location", Intent.Informer, result);

        Assert.Contains("Showing nearest 1 of 30 in radius", Summary(formatted));
    }

    [Fact]
    public void Format_InformerLocationListEmpty_ReturnsNoneFoundSummary()
    {
        var result = Parse("""{"informers":[],"truncated":false,"totalMatchesInRadius":0}""");

        var formatted = ResultFormatter.Format("get_informers_near_location", Intent.Informer, result);

        Assert.Equal("No informers found within the requested radius.", Summary(formatted));
    }
}
