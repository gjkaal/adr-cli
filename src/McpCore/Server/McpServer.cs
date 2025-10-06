using System.Text.Json;

using McpCore.JsonRpc;
using McpCore.Protocol;

namespace McpCore.Server;

/// <summary>
/// Base implementation of MCP server
/// </summary>
public class McpServer : IMcpServer
{
    private readonly Dictionary<string, Func<object?, Task<object>>> _methodHandlers = new();
    private readonly McpServerInfo _serverInfo;
    private readonly McpServerCapabilities _capabilities;
    private bool _initialized = false;

    public McpServer(McpServerInfo serverInfo, McpServerCapabilities capabilities)
    {
        _serverInfo = serverInfo;
        _capabilities = capabilities;

        RegisterDefaultHandlers();
    }

    public virtual async Task<JsonRpcResponse> ProcessRequestAsync(JsonRpcRequest request)
    {
        try
        {
            if (!_methodHandlers.TryGetValue(request.Method, out var handler))
            {
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = JsonRpcErrorCodes.MethodNotFound,
                        Message = $"Method '{request.Method}' not found"
                    }
                };
            }

            var result = await handler(request.Params);

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (ArgumentException ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InvalidParams,
                    Message = ex.Message
                }
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InternalError,
                    Message = ex.Message
                }
            };
        }
    }

    public virtual Task<McpInitializeResult> InitializeAsync(McpInitializeParams parameters)
    {
        _initialized = true;

        return Task.FromResult(new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            ServerInfo = _serverInfo,
            Capabilities = _capabilities
        });
    }

    public virtual Task<McpToolsListResult> GetToolsListAsync()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Server not initialized");
        }

        return Task.FromResult(new McpToolsListResult
        {
            Tools = GetAvailableTools()
        });
    }

    public virtual Task<McpToolCallResult> CallToolAsync(McpToolCallParams parameters)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Server not initialized");
        }

        throw new NotImplementedException("Tool call implementation must be provided by derived class");
    }

    protected virtual McpTool[] GetAvailableTools()
    {
        return Array.Empty<McpTool>();
    }

    protected void RegisterMethodHandler(string method, Func<object?, Task<object>> handler)
    {
        _methodHandlers[method] = handler;
    }

    private void RegisterDefaultHandlers()
    {
        RegisterMethodHandler(McpMethods.Initialize, async (params_) =>
        {
            var parameters = DeserializeParams<McpInitializeParams>(params_);
            return await InitializeAsync(parameters);
        });

        RegisterMethodHandler(McpMethods.ToolsList, async (_) =>
        {
            return await GetToolsListAsync();
        });

        RegisterMethodHandler(McpMethods.ToolsCall, async (params_) =>
        {
            var parameters = DeserializeParams<McpToolCallParams>(params_);
            return await CallToolAsync(parameters);
        });
    }

    private static T DeserializeParams<T>(object? params_)
    {
        if (params_ is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element, N2ConfigurationExtensions.options)
                   ?? throw new ArgumentException($"Failed to deserialize parameters to {typeof(T).Name}");
        }

        if (params_ is T directParams)
        {
            return directParams;
        }

        throw new ArgumentException($"Invalid parameters type for {typeof(T).Name}");
    }
}