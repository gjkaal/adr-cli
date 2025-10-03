using System.Text.Json.Serialization;

namespace McpCore.JsonRpc;

/// <summary>
/// JSON-RPC 2.0 request message
/// </summary>
public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;
    
    [JsonPropertyName("params")]
    public object? Params { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 response message
/// </summary>
public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("result")]
    public object? Result { get; init; }
    
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 error object
/// </summary>
public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    
    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

/// <summary>
/// JSON-RPC 2.0 notification message (no response expected)
/// </summary>
public record JsonRpcNotification
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;
    
    [JsonPropertyName("params")]
    public object? Params { get; init; }
}

/// <summary>
/// Standard JSON-RPC error codes
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
}