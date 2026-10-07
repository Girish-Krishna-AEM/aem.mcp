using System.Text.Json;

namespace CoordinatorAgent.Services;

public interface IMcpClient
{
    Task<JsonElement> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
