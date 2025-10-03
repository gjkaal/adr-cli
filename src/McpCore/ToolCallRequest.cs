using System.Text.Json;

namespace McpCore;

public class ToolCallRequest
{
    public string Name { get; set; } = string.Empty;
    public JsonElement? Arguments { get; set; }
}
