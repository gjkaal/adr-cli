using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.CommandHandlers;

public interface IProjectPlanning
{
    /// <summary>
    /// Initialize a new task record for the current project planning
    /// </summary>
    /// <param name="title">
    /// The title for the task.
    /// </param>
    /// <param name="description">
    /// The consice description for the task.
    /// </param>
    /// <param name="dueDate">
    /// The planned due date for the task.
    /// </param>
    /// <returns>
    /// Response indicating success or failure, with an optional message
    /// </returns>
    Task<Response> NewTaskAsync(string title, string description, string? dueDate);

    /// <summary>
    /// Update the task status to a new state.
    /// </summary>
    /// <param name="sourceId">
    /// A numeric reference to an existing task.
    /// </param>
    /// <param name="status">
    /// The new status for the record.
    /// </param>
    /// <param name="justification">
    /// The expplanation for the new status.
    /// </param>
    /// <returns>
    /// Response with success / failure an optional message.
    /// </returns>
    Task<Response> UpdateTaskAsync(string sourceId, PlanningStatus status, string justification);

    /// <summary>
    /// Show a list with all tasks with their creation date and current state.
    /// </summary>
    /// <param name="sortReverse">
    /// Show project planning in reverse order (newest first).
    /// </param>
    /// <param name="verbose">
    /// Show project planning with more details.
    /// </param>
    /// <returns>
    /// Response with success / failure and the requested list.
    /// </returns>
    Task<Response> ListTasksAsync(bool sortReverse, bool verbose);

    /// <summary>
    /// Show a list with tasks using a filter on title and content.
    /// </summary>
    /// <param name="filter">
    /// A set of words that should be present in the ADR.
    /// </param>
    /// <param name="status">
    /// Only include tasks with the requested status, ignored for status=None.
    /// </param>
    /// <param name="includeContent">
    /// search words in metadata and in content (slower).
    /// </param>
    /// <param name="sortReverse">
    /// Show ADR in reverse order (newest first.
    /// </param>
    /// <param name="verbose">
    /// Show ADR with more details.
    /// </param>
    /// <returns>
    /// integer indicating success or failure
    /// </returns>
    Task<Response> FindTasksAsync(string filter, PlanningStatus status, bool sortReverse, bool verbose, bool includeContent);

    /// <summary>
    /// Add a link between two records. Examples:
    /// <list type="bullet">
    /// <item>link 5 Amend 4</item>
    /// <item>link 2 Replaced-by 5</item>
    /// </list>
    /// </summary>
    /// <param name="sourceId">
    /// The record that relates another record.
    /// </param>
    /// <param name="remark">
    /// The keyword that explains the link.
    /// </param>
    /// <param name="targetId">
    /// The record that is linked to this record.
    /// </param>
    /// <returns>
    /// </returns>
    Task<Response> LinkTaskAsync(int sourceId, int targetId, string remark);

    /// <summary>
    /// Remove all links from the source to the target (reverse links are not removed).
    /// </summary>
    /// <param name="sourceId">
    /// The record that extends another record.
    /// </param>
    /// <param name="targetId">
    /// The record that is linked to this record.
    /// </param>
    /// <returns>
    /// </returns>
    Task<Response> RemoveTaskLinkAsync(int sourceId, int targetId);

    /// <summary>
    /// Generate a table of content markdown file in the configuration root, next to the config file.
    /// </summary>
    Task<Response> GeneratePlanningTocAsync();
}
