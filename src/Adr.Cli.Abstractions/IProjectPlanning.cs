using Adr.Cli.Sync;

using System.Collections.Generic;
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
    /// <param name="useAi">
    /// Draft the Description (if not supplied) and Details using the configured AI provider. A
    /// no-op when no provider is configured.
    /// </param>
    /// <returns>
    /// Response indicating success or failure, with an optional message
    /// </returns>
    Task<Response> NewTaskAsync(string title, string description, string? dueDate, bool useAi);

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

    /// <summary>
    /// Export tasks to the currently configured sync provider, creating or updating each task's
    /// external item and pushing its local status via the provider's local-to-external status map.
    /// One task failing does not stop the rest of the batch.
    /// </summary>
    /// <param name="taskIds">
    /// Explicit task ids to export. Takes priority over <paramref name="filter" /> when non-empty.
    /// </param>
    /// <param name="filter">
    /// A <c>task-find</c>-style word filter against title/description, used when
    /// <paramref name="taskIds" /> is empty. At least one of the two is required - unlike import,
    /// export has no all-tasks default.
    /// </param>
    /// <param name="force">
    /// Skip the remote-divergence check for every task in this batch and push local content
    /// regardless, overwriting whatever is on the external item. A deliberate override, not the
    /// default - intended for a single task at a time (see <see cref="ITaskSyncProvider.ExportAsync" />).
    /// </param>
    /// <param name="dryRun">
    /// Compute and report what each task's export would do (create/update/mismatch, status mapping)
    /// without writing anything locally or remotely.
    /// </param>
    Task<Response<SyncBatchResult>> ExportTasksAsync(IReadOnlyList<int> taskIds, string? filter, bool force = false, bool dryRun = false);

    /// <summary>
    /// Import status from the currently configured sync provider for each selected task, mapping it
    /// to a local <c>PlanningStatus</c> via the provider's external-to-local status map. One task
    /// failing does not stop the rest of the batch.
    /// </summary>
    /// <param name="taskIds">
    /// Explicit task ids to import. Takes priority over <paramref name="filter" /> when non-empty.
    /// </param>
    /// <param name="filter">
    /// A <c>task-find</c>-style word filter against title/description, used when
    /// <paramref name="taskIds" /> is empty.
    /// </param>
    /// <param name="dryRun">
    /// Compute and report what each task's import would do (status mapping, content pull/mismatch)
    /// without writing anything locally or remotely - including skipping the marker-refreshing
    /// re-export that would normally follow a safe content pull, and skipping the creation of any
    /// newly discovered task.
    /// </param>
    /// <remarks>
    /// When both <paramref name="taskIds" /> and <paramref name="filter" /> are empty, defaults to
    /// every task carrying a sync link for the currently active provider, plus discovering and
    /// (unless <paramref name="dryRun" />) adopting board items with no local counterpart yet. A
    /// task whose only sync link belongs to a different, no-longer-active provider is reported as
    /// skipped, not imported.
    /// </remarks>
    Task<Response<SyncBatchResult>> ImportTaskStatusAsync(IReadOnlyList<int> taskIds, string? filter, bool dryRun = false);
}
