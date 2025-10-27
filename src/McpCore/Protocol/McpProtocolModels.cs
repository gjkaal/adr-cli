using System.Text.Json.Serialization;

namespace McpCore.Protocol;

/// <summary>
/// MCP initialize request parameters
/// </summary>
public record McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public McpClientCapabilities? Capabilities { get; init; }

    [JsonPropertyName("clientInfo")]
    public McpClientInfo? ClientInfo { get; init; }
}

/// <summary>
/// MCP client information
/// </summary>
public record McpClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}

/// <summary>
/// MCP client capabilities
/// </summary>
public record McpClientCapabilities
{
    [JsonPropertyName("experimental")]
    public Dictionary<string, object>? Experimental { get; init; }
}

/// <summary>
/// MCP initialize response result
/// </summary>
public record McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public McpServerCapabilities Capabilities { get; init; } = new();

    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; init; } = new();
}

/// <summary>
/// MCP server capabilities
/// </summary>
public record McpServerCapabilities
{
    [JsonPropertyName("tools")]
    public McpToolsCapability? Tools { get; init; } = new();

    [JsonPropertyName("experimental")]
    public Dictionary<string, object>? Experimental { get; init; }
}

/// <summary>
/// MCP tools capability
/// </summary>
public record McpToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; } = false;
}

/// <summary>
/// MCP server information
/// </summary>
public record McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}

/// <summary>
/// MCP tool call parameters
/// </summary>
public record McpToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; init; } = new();
}

/// <summary>
/// MCP tool definition
/// </summary>
public record McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public McpInputSchema InputSchema { get; init; } = new();
}

/// <summary>
/// MCP tool input schema (JSON Schema)
/// </summary>
public record McpInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpPropertyDefinition> Properties { get; init; } = new();

    [JsonPropertyName("required")]
    public string[] Required { get; init; } = Array.Empty<string>();

    [JsonPropertyName("additionalProperties")]
    public bool AdditionalProperties { get; init; } = false;
}

/// <summary>
/// MCP property definition for input schema
/// </summary>
public record McpPropertyDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("enum")]
    public string[]? Enum { get; init; }

    [JsonPropertyName("default")]
    public object? Default { get; init; }
}

/// <summary>
/// MCP tools list response
/// </summary>
public record McpToolsListResult
{
    [JsonPropertyName("tools")]
    public McpTool[] Tools { get; init; } = Array.Empty<McpTool>();
}

/// <summary>
/// MCP tool call result
/// </summary>
public record McpToolCallResult
{
    [JsonPropertyName("content")]
    public McpContent[] Content { get; init; } = Array.Empty<McpContent>();

    [JsonPropertyName("isError")]
    public bool IsError { get; init; } = false;
}

/// <summary>
/// MCP content item
/// </summary>
public record McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}

/// <summary>
/// MCP method names
/// </summary>
public static class McpMethods
{
    public const string Initialize = "initialize";
    public const string Initialized = "notifications/initialized";
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
}