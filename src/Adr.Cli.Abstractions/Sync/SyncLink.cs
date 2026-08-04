using System;

namespace Adr.Cli.Sync;

/// <summary>
/// Whether a task's link to an external provider currently has a resolvable status mapping.
/// Tracked separately from <see cref="PlanningStatus" /> so sync health never contaminates the
/// task's actual workflow state.
/// </summary>
public enum SyncState
{
    /// <summary>
    /// The last export or import could not resolve a status mapping (an external or local status
    /// had no entry in the configured status map) and the task's status was left unchanged rather
    /// than guessed. The zero value, so a link that has never synced also reads as unmapped -
    /// important because the repository's JSON options omit properties equal to their type's
    /// default, so a persisted <see cref="Synced" /> link must never be the zero value or it would
    /// round-trip back as whatever value 0 maps to.
    /// </summary>
    Unmapped = 0,

    /// <summary>
    /// The task's local and external status were both resolvable through the configured maps as
    /// of <see cref="SyncLink.LastSyncedAt" />.
    /// </summary>
    Synced = 1,

    /// <summary>
    /// Content (title/description) diverged on both the local and external side since the last
    /// successful sync, detected via the content hash marker - see
    /// <see cref="Adr.Cli.Sync.ContentSyncMarker" />. Neither side was touched; resolving this
    /// requires a person to reconcile the two versions manually.
    /// </summary>
    Mismatch = 2
}

/// <summary>
/// Records a task's association with a single external work item for one provider. Export
/// operations use this to update the existing external item rather than create a duplicate;
/// import operations use it to locate the external item to read status from.
/// </summary>
public class SyncLink
{
    /// <summary>
    /// The connector this link belongs to (e.g. "AzureDevOps", "GitHubProjects"). Only one
    /// provider is active per repository at a time; a link whose provider does not match the
    /// currently configured provider is inert - skipped by <c>task-import</c>'s default scope, not
    /// treated as an error.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The external organization/project (Azure DevOps) or owner/project number (GitHub Projects)
    /// this task was exported to, recorded for traceability. Scope is a single global,
    /// provider-configured value for the whole repository - this field is not an override point.
    /// </summary>
    public string ExternalScope { get; set; } = string.Empty;

    /// <summary>
    /// The external work-item, issue, or project-item identifier. Empty until the first successful
    /// export.
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// The underlying content type of the external item, when the provider distinguishes one (e.g.
    /// GitHub Projects: "DraftIssue", "Issue", "PullRequest"). A GitHub draft issue promoted to a
    /// real issue on the board keeps the same <see cref="ExternalId" /> (the project item itself
    /// doesn't change identity) but this reflects the change, since a real issue can no longer have
    /// its content pushed from here without repository write permissions - see ADR 00009.
    /// </summary>
    public string ExternalContentType { get; set; } = string.Empty;

    /// <summary>
    /// A browsable URL for the external item, refreshed on every successful export/import.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Whether the last export/import resolved a status mapping for this task.
    /// </summary>
    public SyncState SyncState { get; set; } = SyncState.Unmapped;

    /// <summary>
    /// When this link was last updated by a successful export or import.
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
}
