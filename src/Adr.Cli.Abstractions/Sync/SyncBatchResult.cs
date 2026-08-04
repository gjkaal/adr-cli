using System.Collections.Generic;
using System.Linq;

namespace Adr.Cli.Sync;

public enum SyncItemOutcome
{
    Succeeded,

    /// <summary>
    /// The operation completed, but a status mapping could not be resolved, so the task's sync
    /// state was left <see cref="SyncState.Unmapped" /> rather than guessed. Distinct from
    /// <see cref="Succeeded" /> so an unmapped status is never silently reported as a clean import.
    /// </summary>
    Unmapped,

    Failed,

    /// <summary>
    /// Not attempted - e.g. the task's sync link belongs to a provider other than the currently
    /// active one, or no sync link exists for the active provider. Distinct from <see cref="Failed" />
    /// so a stale link from a previously-configured connector doesn't read as an error.
    /// </summary>
    Skipped,

    /// <summary>
    /// Content diverged on both sides since the last sync (see <see cref="SyncState.Mismatch" />)
    /// - neither side was touched, and a person needs to reconcile the two versions manually.
    /// </summary>
    Mismatch
}

/// <summary>
/// The outcome of one task within a <c>task-export</c>/<c>task-import</c> batch. One failed or
/// skipped task never blocks the rest of the batch from being processed and reported.
/// </summary>
public class SyncItemResult
{
    public int RecordId { get; set; }
    public string Title { get; set; } = string.Empty;
    public SyncItemOutcome Outcome { get; set; }
    public string? Message { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalUrl { get; set; }
}

/// <summary>
/// Aggregate result of a batch <c>task-export</c>/<c>task-import</c> operation, with each task's
/// outcome reported individually.
/// </summary>
public class SyncBatchResult
{
    public List<SyncItemResult> Items { get; set; } = new();

    public int SucceededCount => Items.Count(item => item.Outcome == SyncItemOutcome.Succeeded);
    public int UnmappedCount => Items.Count(item => item.Outcome == SyncItemOutcome.Unmapped);
    public int FailedCount => Items.Count(item => item.Outcome == SyncItemOutcome.Failed);
    public int SkippedCount => Items.Count(item => item.Outcome == SyncItemOutcome.Skipped);
    public int MismatchCount => Items.Count(item => item.Outcome == SyncItemOutcome.Mismatch);
}
