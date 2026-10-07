using System.Text.Json;
using CoordinatorAgent.Models;

namespace CoordinatorAgent.Services;

public interface IParameterExtractor
{
    Task<(bool Success, JsonElement? Parameters, string? Error)> ExtractParameters(string query, Intent intent);
}
