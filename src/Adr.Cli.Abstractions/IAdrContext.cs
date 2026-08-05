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
    /// Re-resolve settings from an adr.config.json, identified either by a directory (searched
    /// upward first, then downward into subfolders if nothing is found) or by project name. Never
    /// creates a config file or ADR folders. If more than one candidate matches, the response lists
    /// each one's project name and folder path instead of guessing - call this again with one of
    /// them.
    /// </summary>
    /// <param name="workingDirectoryOrProjectName">
    /// Any directory inside (or above) the target ADR repository, a workspace root to search
    /// downward from, or an ADR project's name.
    /// </param>
    Task<Response> SetContextAsync(string workingDirectoryOrProjectName);
}
