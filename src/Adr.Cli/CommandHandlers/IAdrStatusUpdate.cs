using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handlers for managing record state.
/// </summary>
public interface IAdrStatusUpdate
{
    /// <summary>
    /// Change the status to 'Proposed'. The state change is valid when the
    /// current state is New or Final.
    /// </summary>
    /// <param name="record">A record identification.</param>
    /// <param name="remark">An optional remark that will be added to the document.</param>
    /// <returns>0 is the task completed successful</returns>
    Task<int> SetProposed(int record, string remark);

    /// <summary>
    /// Change the status to 'Final'. The state change is valid when the
    /// current state is New or Proposed.
    /// </summary>
    /// <param name="record">A record identification.</param>
    /// <param name="remark">An optional remark that will be added to the document.</param>
    /// <returns>0 is the task completed successful</returns>
    Task<int> SetFinal(int record, string remark);

    /// <summary>
    /// Change the status to 'Accepted'. This is an end state. An ADR with a state
    /// 'Accepted' should not be modified, except for a change of state to obsolete.
    /// The metadata file and the document will be changed to read only.
    /// The state change is valid when the current state is New, Proposed, Final or Accepted.
    /// In any case, the file state will be set to read only.
    /// </summary>
    /// <param name="record">A record identification.</param>
    /// <param name="remark">An optional remark that will be added to the document.</param>
    /// <returns>0 is the task completed successful</returns>
    Task<int> SetAccepted(int record, string remark);

    /// <summary>
    /// Change the status to 'Obsolete'. This is an end state. No changes to any other state 
    /// are allowed. If an ADR should be reused, use a copy command to restart the ADR lifetime.
    /// </summary>
    /// <param name="record">A record identification.</param>
    /// <param name="remark">An optional remark that will be added to the document.</param>
    /// <returns>0 is the task completed successful</returns>
    Task<int> SetObsolete(int record, string remark);
}