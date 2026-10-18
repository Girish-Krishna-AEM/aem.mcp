# `get_informer_status` Tool Contract Update — Code Summary (FR-6, Unit 1)

## Modified files
- `unit-1/src/LightningMcpServer/Mcp/McpTools.cs` — `get_informer_status` tool parameters changed from `informerId?`/`zone?` to `search` (required), `startDateTimeUtc?`/`endDateTimeUtc?` (epoch seconds); return type changed from `InformerStatus` to `InformerStatusResult`.
- `unit-1/src/LightningMcpServer/Mcp/ToolRegistry.cs` — `ListTools()`'s `get_informer_status` `inputSchema` updated to match (`search` required string, `startDateTimeUtc`/`endDateTimeUtc` optional numbers); `HandleInformerStatus` rebuilds `GetInformerStatusRequest` from the new argument shape.

Tool name is unchanged (`get_informer_status`) — only the input/output contract changed, per BR-13.
