using Adr.Cli.Sync;

using System.Collections.Generic;
using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for syncing ADRs to/from the currently configured GitHub connector as real
/// repository Issues, with each ADR's related Tasks (<see cref="AdrRecord.RelatedTasks" />) attached
/// as GitHub sub-issues. See ADR 00010. Named <c>adr-export</c>/<c>adr-import</c>, not "sync", for the
/// same reason task sync avoids that word (ADR 00008) - "sync" already names the unrelated
/// markdown-to-metadata resynchronization command.
/// </summary>
public interface IAdrGitHubSync
{
    /// <summary>
    /// Export ADRs to the currently configured sync provider: create or update each ADR's external
    /// Issue, push its local status, and attach each related task as a GitHub sub-issue (promoting it
    /// to a real Issue first if needed). One ADR failing does not stop the rest of the batch.
    /// </summary>
    /// <param name="adrIds">
    /// Explicit ADR ids to export. Takes priority over <paramref name="filter" /> when non-empty.
    /// </param>
    /// <param name="filter">
    /// An <c>adr-find</c>-style word filter against title/context, used when <paramref name="adrIds" />
    /// is empty. At least one of the two is required - there is no all-ADRs default.
    /// </param>
    /// <param name="force">
    /// Skip the remote-divergence check for every ADR in this batch and push local content
    /// regardless. A deliberate override, not the default - intended for a single ADR at a time.
    /// </param>
    /// <param name="dryRun">
    /// Compute and report what each ADR's export (and each related task's promotion/attachment) would
    /// do, without writing anything locally or remotely.
    /// </param>
    Task<Response<SyncBatchResult>> ExportAdrAsync(IReadOnlyList<int> adrIds, string? filter, bool force = false, bool dryRun = false);

    /// <summary>
    /// Import status - and, when safe, content - from the currently configured sync provider for
    /// each selected ADR. One ADR failing does not stop the rest of the batch.
    /// </summary>
    /// <param name="adrIds">
    /// Explicit ADR ids to import. Takes priority over <paramref name="filter" /> when non-empty.
    /// </param>
    /// <param name="filter">
    /// An <c>adr-find</c>-style word filter against title/context, used when <paramref name="adrIds" />
    /// is empty.
    /// </param>
    /// <param name="dryRun">
    /// Compute and report what each ADR's import would do without writing anything locally or
    /// remotely.
    /// </param>
    /// <remarks>
    /// When both <paramref name="adrIds" /> and <paramref name="filter" /> are empty, defaults to
    /// every ADR carrying a sync link for the currently active provider. Unlike <c>task-import</c>,
    /// there is no discovery/adoption of board-only items as brand-new local ADRs - see ADR 00010.
    /// </remarks>
    Task<Response<SyncBatchResult>> ImportAdrStatusAsync(IReadOnlyList<int> adrIds, string? filter, bool dryRun = false);
}
