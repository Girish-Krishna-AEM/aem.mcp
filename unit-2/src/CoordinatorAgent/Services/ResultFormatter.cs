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
                Intent.Informer => BuildInformerSummary(result),
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

    private static string BuildSensorSummary(JsonElement result)
    {
        var status = result.GetProperty("status").GetString();
        var efficiency = result.GetProperty("detectionEfficiency").GetDouble();
        return $"Sensor status: {status}, detection efficiency {efficiency:F1}%.";
    }

    private static string BuildInformerSummary(JsonElement result)
    {
        var status = result.GetProperty("status").GetString();
        var power = result.GetProperty("powerStatus").GetString();
        return $"Informer status: {status}, power: {power}.";
    }
}
