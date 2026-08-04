using Adr.Cli.CommandHandlers;

using McpCore;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Adr.Cli.Sync;

/// <summary>
/// Result of exporting a task to an external provider.
/// </summary>
public class TaskExportResult
{
    /// <summary>
    /// True when a new external item was created; false when an existing one (identified by the
    /// task's <see cref="SyncLink" /> for this provider) was updated instead.
    /// </summary>
    public bool Created { get; set; }

    public string ExternalScope { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// The external item's underlying content type, when the provider distinguishes one (e.g. GitHub
    /// Projects: "DraftIssue", "Issue", "PullRequest") - see <see cref="SyncLink.ExternalContentType" />.
    /// </summary>
    public string ExternalContentType { get; set; } = string.Empty;

    /// <summary>
    /// A browsable URL for the external item, when the provider can supply one.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Whether the push actually happened. <see cref="SyncState.Synced" /> or
    /// <see cref="SyncState.Unmapped" /> both mean content was pushed (the latter only means the
    /// local status had no entry in the export status map). <see cref="SyncState.Mismatch" />
    /// means the push was refused because the remote item had diverged since the last sync - neither
    /// side was touched.
    /// </summary>
    public SyncState SyncState { get; set; }
}

/// <summary>
/// Result of importing a task's status from an external provider.
/// </summary>
public class TaskImportResult
{
    /// <summary>
    /// The local status resolved via the provider's external-to-local status map, or
    /// <see langword="null" /> when <see cref="SyncState" /> is <see cref="SyncState.Unmapped" />.
    /// </summary>
    public PlanningStatus? MappedStatus { get; set; }

    /// <summary>
    /// The raw external status value, kept for diagnostics regardless of whether it mapped.
    /// </summary>
    public string? ExternalStatusRaw { get; set; }

    /// <summary>
    /// See <see cref="TaskExportResult.ExternalContentType" />. Refreshed on every import so a draft
    /// issue promoted to a real issue on the board is reflected locally.
    /// </summary>
    public string ExternalContentType { get; set; } = string.Empty;

    /// <summary>
    /// See <see cref="TaskExportResult.ExternalUrl" />.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// <see cref="SyncState.Synced" />/<see cref="SyncState.Unmapped" /> describe the status
    /// mapping outcome as before. <see cref="SyncState.Mismatch" /> means local content changed
    /// since the last sync while remote also diverged - content was left untouched on both sides
    /// (status may still have been read; see <see cref="MappedStatus" />).
    /// </summary>
    public SyncState SyncState { get; set; }

    /// <summary>
    /// Set only when content was safely pulled from the remote side (local hadn't diverged since the
    /// last sync, per <see cref="ContentSyncMarker.ResolveImport" />). The caller should apply these
    /// to the local task and re-export to refresh the content hash marker.
    /// </summary>
    public string? PulledTitle { get; set; }

    /// <summary>
    /// See <see cref="PulledTitle" />.
    /// </summary>
    public string? PulledBody { get; set; }
}

/// <summary>
/// A provider-agnostic connector for exporting tasks to, and importing task status from, a single
/// external work-management application. Implementations own all provider-specific behavior -
/// authentication, API communication, field translation, and status mapping - so the core task
/// model and the shared <c>task-export</c>/<c>task-import</c> services never need provider-specific
/// logic. Only one provider is active per repository at a time (see
/// <see cref="SyncLink.Provider" />); when none is configured, the registered implementation is
/// a no-op that always fails, so callers never need a null check.
/// </summary>
public interface ITaskSyncProvider
{
    /// <summary>
    /// The connector name this instance implements (e.g. "AzureDevOps", "GitHubProjects"). Compared
    /// against <see cref="SyncLink.Provider" /> and the configured active provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Create or update the external item for <paramref name="task" />, and push its
    /// <see cref="TaskRecord.Status" /> using this provider's local-to-external status map.
    /// </summary>
    /// <param name="task">
    /// The task to export. Its title, description, and details are the source of truth for the
    /// external item's content.
    /// </param>
    /// <param name="existingLink">
    /// The task's existing sync link for this provider, if any. When <see langword="null" /> or its
    /// <see cref="SyncLink.ExternalId" /> is empty, a new external item is created; otherwise the
    /// existing one is updated.
    /// </param>
    /// <param name="force">
    /// Skip the remote-divergence check and push local content regardless, overwriting whatever is
    /// currently on the external item. An explicit, deliberate override for a single task - never
    /// the default - for when a person has already looked at both sides and decided local should
    /// win, rather than resolving a <see cref="SyncState.Mismatch" /> by hand.
    /// </param>
    /// <param name="dryRun">
    /// Perform every read needed to compute the real outcome (project/field resolution, the
    /// remote-divergence check) but skip every mutation - nothing is created, updated, or field-set
    /// on the external side. The returned result describes what would have happened; a create is
    /// reported with an empty <see cref="TaskExportResult.ExternalId" /> since no real item exists to
    /// report an id for.
    /// </param>
    Task<Response<TaskExportResult>> ExportAsync(TaskRecord task, SyncLink? existingLink, bool force = false, bool dryRun = false);

    /// <summary>
    /// Read the current external status for <paramref name="task" /> and map it to a local status
    /// using this provider's external-to-local status map.
    /// </summary>
    /// <param name="existingLink">
    /// The task's sync link for this provider, identifying which external item to read.
    /// </param>
    /// <param name="dryRun">
    /// Import is already read-only for status/content resolution; this only affects the caller's
    /// behavior (e.g. skipping the local persistence and the marker-refreshing re-export that would
    /// normally follow a safe content pull) - implementations may accept and ignore it if they have
    /// no additional side effect to suppress.
    /// </param>
    Task<Response<TaskImportResult>> ImportAsync(TaskRecord task, SyncLink existingLink, bool dryRun = false);

    /// <summary>
    /// List every item currently on the provider's board/list, for discovering ones with no local
    /// counterpart yet (e.g. a task created directly on GitHub Projects rather than exported from
    /// here). The caller matches these against local tasks by title - the provider has no visibility
    /// into local tasks, so it returns every item rather than filtering itself.
    /// </summary>
    Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync();
}
