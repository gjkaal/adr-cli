using Adr.Cli.CommandHandlers;

using McpCore;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Adr.Cli.Sync;

/// <summary>
/// Result of exporting an ADR to an external provider. Mirrors <see cref="TaskExportResult" /> - kept
/// as a separate type rather than shared, since <see cref="AdrImportResult.MappedStatus" /> is an
/// <see cref="AdrStatus" /> rather than a <see cref="PlanningStatus" />. See ADR 00010.
/// </summary>
public class AdrExportResult
{
    /// <summary>
    /// True when a new external item was created; false when an existing one (identified by the
    /// ADR's <see cref="SyncLink" /> for this provider) was updated instead.
    /// </summary>
    public bool Created { get; set; }

    public string ExternalScope { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// The external item's underlying content type - for GitHub, always "Issue" for ADRs, since ADR
    /// export always creates a repository-backed Issue, never a Draft Issue (see ADR 00010).
    /// </summary>
    public string ExternalContentType { get; set; } = string.Empty;

    /// <summary>
    /// A browsable URL for the external item, when the provider can supply one.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// The external item's own node id (as opposed to a project-item id), needed to attach related
    /// tasks as sub-issues via <see cref="IAdrSyncProvider.EnsureSubIssueAsync" />.
    /// </summary>
    public string? IssueNodeId { get; set; }

    /// <summary>
    /// See <see cref="TaskExportResult.SyncState" /> - identical semantics, against
    /// <see cref="AdrStatus" /> instead of <see cref="PlanningStatus" />.
    /// </summary>
    public SyncState SyncState { get; set; }
}

/// <summary>
/// Result of importing an ADR's status from an external provider. Mirrors <see cref="TaskImportResult" />.
/// </summary>
public class AdrImportResult
{
    /// <summary>
    /// The local status resolved via the provider's external-to-local status map, or
    /// <see langword="null" /> when <see cref="SyncState" /> is <see cref="SyncState.Unmapped" />.
    /// </summary>
    public AdrStatus? MappedStatus { get; set; }

    public string? ExternalStatusRaw { get; set; }

    public string ExternalContentType { get; set; } = string.Empty;

    public string? ExternalUrl { get; set; }

    public SyncState SyncState { get; set; }

    /// <summary>
    /// Set only when content was safely pulled from the remote side - see
    /// <see cref="ContentSyncMarker.ResolveImport" />. The caller should split this via
    /// <see cref="AdrBodyFormat.Split" />, apply the result to the local ADR, and re-export to
    /// refresh the content hash marker.
    /// </summary>
    public string? PulledTitle { get; set; }

    /// <summary>
    /// See <see cref="PulledTitle" />.
    /// </summary>
    public string? PulledBody { get; set; }
}

/// <summary>
/// Result of ensuring a related task is a real repository Issue and attaching it as a GitHub
/// sub-issue of its ADR's Issue. See <see cref="IAdrSyncProvider.EnsureSubIssueAsync" /> and ADR 00010.
/// </summary>
public class TaskPromotionResult
{
    /// <summary>
    /// True when this call created a new repository Issue or converted an existing Draft Issue into
    /// one; false when the task was already a real Issue/PR and was reused as-is.
    /// </summary>
    public bool Promoted { get; set; }

    /// <summary>
    /// The task's project-item id - unchanged across a Draft Issue -> Issue promotion, so the task's
    /// existing <see cref="SyncLink" />/content marker stays valid.
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    public string ExternalContentType { get; set; } = string.Empty;

    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Whether the sub-issue attachment mutation was actually invoked. False for a dry run.
    /// </summary>
    public bool SubIssueLinked { get; set; }
}

/// <summary>
/// A provider-agnostic connector for exporting ADRs to, and importing ADR status from, a single
/// external work-management application, and for attaching an ADR's related Tasks to it as sub-issues.
/// Mirrors <see cref="ITaskSyncProvider" />'s shape rather than generalizing it over both record
/// kinds, since sub-issue attachment has no task-side equivalent. Only one provider is active per
/// repository at a time (the same one configured for task sync - see ADR 00010); when none is
/// configured, the registered implementation is a no-op that always fails, so callers never need a
/// null check.
/// </summary>
public interface IAdrSyncProvider
{
    /// <summary>
    /// The connector name this instance implements (e.g. "GitHubProjects"). Compared against
    /// <see cref="SyncLink.Provider" /> and the configured active provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Create or update the external item for <paramref name="adr" />, and push its
    /// <see cref="AdrRecord.Status" /> using this provider's local-to-external status map.
    /// </summary>
    /// <param name="adr">
    /// The ADR to export. Its title, context, decision, and consequences are the source of truth for
    /// the external item's content.
    /// </param>
    /// <param name="existingLink">
    /// The ADR's existing sync link for this provider, if any. When <see langword="null" /> or its
    /// <see cref="SyncLink.ExternalId" /> is empty, a new external item is created; otherwise the
    /// existing one is updated.
    /// </param>
    /// <param name="force">
    /// Skip the remote-divergence check and push local content regardless - see
    /// <see cref="ITaskSyncProvider.ExportAsync" /> for the equivalent task-side rationale.
    /// </param>
    /// <param name="dryRun">
    /// Perform every read needed to compute the real outcome but skip every mutation.
    /// </param>
    Task<Response<AdrExportResult>> ExportAsync(AdrRecord adr, SyncLink? existingLink, bool force = false, bool dryRun = false);

    /// <summary>
    /// Read the current external status for <paramref name="adr" /> and map it to a local status
    /// using this provider's external-to-local status map.
    /// </summary>
    Task<Response<AdrImportResult>> ImportAsync(AdrRecord adr, SyncLink existingLink, bool dryRun = false);

    /// <summary>
    /// List every ADR item currently on the provider's board/list, for discovering ones with no local
    /// counterpart yet.
    /// </summary>
    Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync();

    /// <summary>
    /// Ensure <paramref name="task" /> is a real repository-backed Issue - creating one directly, or
    /// promoting its existing Draft Issue in place, as needed (see ADR 00010) - and attach it as a
    /// GitHub sub-issue of the ADR Issue identified by <paramref name="adrIssueNodeId" />. Reuses the
    /// task's existing Issue/PR link as-is if it's already promoted. One-way: never demotes a promoted
    /// task back to a Draft Issue.
    /// </summary>
    /// <param name="adrIssueNodeId">
    /// The ADR's own Issue node id (<see cref="AdrExportResult.IssueNodeId" />), not its project-item
    /// id - sub-issue attachment links Issue-to-Issue.
    /// </param>
    /// <param name="task">
    /// The related task to promote/attach.
    /// </param>
    /// <param name="existingTaskLink">
    /// The task's existing sync link for this provider, if any.
    /// </param>
    /// <param name="dryRun">
    /// Compute and report the outcome without creating, promoting, or attaching anything.
    /// </param>
    Task<Response<TaskPromotionResult>> EnsureSubIssueAsync(string adrIssueNodeId, TaskRecord task, SyncLink? existingTaskLink, bool dryRun = false);
}
