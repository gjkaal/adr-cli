using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for verifying and switching which adr.config.json is active for this process.
/// See ADR 00004 (Configurable ADR root context for MCP tool calls).
/// </summary>
public interface IAdrContext
{
    /// <summary>
    /// Report which adr.config.json is currently active, without changing anything.
    /// </summary>
    Task<Response> GetContextAsync();

    /// <summary>
    /// Re-resolve settings from the adr.config.json found by searching upward from
    /// <paramref name="workingDirectory" />. Never creates a config file or ADR folders.
    /// </summary>
    /// <param name="workingDirectory">
    /// Any directory inside the target ADR repository.
    /// </param>
    Task<Response> SetContextAsync(string workingDirectory);
}
