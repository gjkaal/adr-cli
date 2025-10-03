using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handlers for managing record state.
/// </summary>
public interface IAdrStatusUpdate
{
    /// <summary>
    /// Change the status toa new status. The state change is validated based on the current status.
    /// </summary>
    /// <param name="record">A record identification.</param>
    /// <param name="remark">An optional remark that will be added to the document.</param>
    /// <returns>0 is the task completed successful</returns>
    Task<Response> SetStatus(int record, AdrStatus status, string remark);
}