using McpCore.JsonRpc;
using McpCore.Protocol;

namespace McpCore.Server;

/// <summary>
/// Interface for MCP server functionality
/// </summary>
public interface IMcpServer
{
    /// <summary>
    /// Process a JSON-RPC request and return a response
    /// </summary>
    Task<JsonRpcResponse> ProcessRequestAsync(JsonRpcRequest request);
    
    /// <summary>
    /// Initialize the MCP server with client capabilities
    /// </summary>
    Task<McpInitializeResult> InitializeAsync(McpInitializeParams parameters);
    
    /// <summary>
    /// Get the list of available tools
    /// </summary>
    Task<McpToolsListResult> GetToolsListAsync();
    
    /// <summary>
    /// Call a specific tool with provided arguments
    /// </summary>
    Task<McpToolCallResult> CallToolAsync(McpToolCallParams parameters);
}