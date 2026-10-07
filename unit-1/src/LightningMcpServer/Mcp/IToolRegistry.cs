using System.Text.Json;

namespace LightningMcpServer.Mcp;

public interface IToolRegistry
{
    JsonElement ListTools();
    JsonElement CallTool(string toolName, JsonElement arguments);
}
