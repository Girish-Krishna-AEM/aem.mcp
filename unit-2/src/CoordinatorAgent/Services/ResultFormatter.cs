using System.Linq;
using System.Text.Json;
using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public static class ResultFormatter
{
    public static object Format(string toolName, Intent intent, JsonElement result)
    {
        return new
        {
            toolName,
            result,
            summary = BuildSummary(intent, result)
        };
    }

    private static string BuildSummary(Intent intent, JsonElement result)
    {
        try
        {
            return intent switch
            {
                Intent.Strike => BuildStrikeSummary(result),
                Intent.Weather => BuildWeatherSummary(result),
                Intent.Sensor => BuildSensorSummary(result),
                Intent.Informer => result.TryGetProperty("informers", out _)
                    ? BuildInformerListSummary(result)
                    : BuildInformerSummary(result),
                _ => "Request completed."
            };
        }
        catch (Exception)
        {
            return "Request completed.";
        }
    }

    private static string BuildStrikeSummary(JsonElement result)
    {
        var count = result.GetProperty("strikeCount").GetInt32();
        var nearest = result.GetProperty("nearestStrikeDistance").GetDouble();
        return count == 0
            ? "No lightning strikes detected in the search area."
            : $"Found {count} lightning strike(s) with the nearest strike {nearest:F2} km away.";
    }

    private static string BuildWeatherSummary(JsonElement result)
    {
        // Real daily/hourly tools return {latitude, longitude, periods: [...]}.
        // The original mock tool returns {forecast: [...]} — keep both shapes supported.
        if (result.TryGetProperty("periods", out var periods))
        {
            var forecastCreatedUtc = result.TryGetProperty("forecastCreatedUtc", out var fcu) ? fcu.GetString() : null;
            return BuildRealForecastSummary(periods, forecastCreatedUtc);
        }

        var forecast = result.GetProperty("forecast");
        var entryCount = forecast.GetArrayLength();
        if (entryCount == 0)
        {
            return "No forecast data available.";
        }

        var first = forecast[0];
        var condition = first.GetProperty("condition").GetString();
        var temp = first.GetProperty("temp").GetInt32();
        return entryCount == 1
            ? $"Forecast: {condition}, {temp}°."
            : $"{entryCount}-day forecast starting with {condition}, {temp}°.";
    }

    private static string BuildRealForecastSummary(JsonElement periods, string? forecastCreatedUtc)
    {
        var periodCount = periods.GetArrayLength();
        if (periodCount == 0)
        {
            return "No forecast data available.";
        }

        var first = periods[0];
        var tempC = first.GetProperty("tempC").GetDouble();
        var precipPct = first.GetProperty("precipPct").GetDouble();
        // Surfaces when the upstream provider generated this forecast (it refreshes periodically,
        // not on every call) so two forecasts fetched minutes apart aren't mistaken for a bug when
        // they differ — see 2026-10-08 audit entry for the incident this was added to resolve.
        var asOf = string.IsNullOrEmpty(forecastCreatedUtc) ? string.Empty : $" (as of {forecastCreatedUtc})";
        return periodCount == 1
            ? $"Forecast: {tempC:F1}°C, {precipPct:F0}% precip chance.{asOf}"
            : $"{periodCount}-period forecast starting at {tempC:F1}°C, {precipPct:F0}% precip chance.{asOf}";
    }

    private static string BuildSensorSummary(JsonElement result)
    {
        var status = result.GetProperty("status").GetString();
        var efficiency = result.GetProperty("detectionEfficiency").GetDouble();
        return $"Sensor status: {status}, detection efficiency {efficiency:F1}%.";
    }

    private static string BuildInformerSummary(JsonElement result)
    {
        var kind = result.GetProperty("kind").GetString();

        return kind switch
        {
            "Found" => BuildInformerFoundSummary(result),
            "Disabled" => $"Informer '{GetMatchInformName(result)}' is disabled and not operational.",
            "Ambiguous" => BuildInformerAmbiguousSummary(result),
            "NotFound" => "No informer found matching that search.",
            _ => "Request completed."
        };
    }

    private static string BuildInformerFoundSummary(JsonElement result)
    {
        var informName = GetMatchInformName(result);
        var siteGroups = result.GetProperty("alertSiteGroups");
        var siteCount = siteGroups.GetArrayLength();
        var alertCount = siteGroups.EnumerateArray().Sum(site => site.GetProperty("alerts").GetArrayLength());

        return alertCount == 0
            ? $"Informer '{informName}': no alerts in the requested time range."
            : $"Informer '{informName}': {alertCount} alert(s) across {siteCount} site(s).";
    }

    private static string BuildInformerAmbiguousSummary(JsonElement result)
    {
        var candidates = result.GetProperty("candidates")
            .EnumerateArray()
            .Select(c => $"{c.GetProperty("informName").GetString()} ({c.GetProperty("custName").GetString()}, {c.GetProperty("macAddress").GetString()})");

        return $"Multiple informers match that search. Did you mean: {string.Join("; ", candidates)}? Please refine your search (e.g. by MAC address or informer ID).";
    }

    private static string BuildInformerListSummary(JsonElement result)
    {
        var informers = result.GetProperty("informers").EnumerateArray().ToList();
        if (informers.Count == 0)
        {
            return "No informers found within the requested radius.";
        }

        var disabledCount = informers.Count(i => i.GetProperty("isDisabled").GetBoolean());
        var totalAlerts = informers.Sum(i => i.GetProperty("alertCount").GetInt32());
        var totalSites = informers.Sum(i => i.GetProperty("siteCount").GetInt32());
        var truncated = result.GetProperty("truncated").GetBoolean();
        var totalMatchesInRadius = result.GetProperty("totalMatchesInRadius").GetInt32();

        var summary = $"Found {informers.Count} informer(s) within the requested radius: {totalAlerts} alert(s) total across {totalSites} site(s).";
        if (disabledCount > 0)
        {
            summary += $" ({disabledCount} disabled.)";
        }
        if (truncated)
        {
            summary += $" (Showing nearest {informers.Count} of {totalMatchesInRadius} in radius.)";
        }

        return summary;
    }

    private static string GetMatchInformName(JsonElement result) =>
        result.GetProperty("match").GetProperty("informName").GetString() ?? "unknown";
}
