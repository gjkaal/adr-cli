using System.Collections.Generic;
using System.Threading.Tasks;

using Adr.Cli.Sync;

using McpCore;

namespace Adr.Cli.Services;

/// <summary>
/// Default <see cref="IAdrSyncProvider" /> registered when no connector is configured in
/// adr.config.json. Always fails, so <c>adr-export</c>/<c>adr-import</c> callers never need a null
/// check.
/// </summary>
public class NoOpAdrSyncProvider : IAdrSyncProvider
{
    public string Name => string.Empty;

    public Task<Response<AdrExportResult>> ExportAsync(AdrRecord adr, SyncLink? existingLink, bool force = false, bool dryRun = false)
    {
        var response = new Response<AdrExportResult>(
            false,
            "No ADR sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            new AdrExportResult());
        return Task.FromResult(response);
    }

    public Task<Response<AdrImportResult>> ImportAsync(AdrRecord adr, SyncLink existingLink, bool dryRun = false)
    {
        var response = new Response<AdrImportResult>(
            false,
            "No ADR sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            new AdrImportResult());
        return Task.FromResult(response);
    }

    public Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync()
    {
        var response = new Response<IReadOnlyList<DiscoveredExternalItem>>(
            false,
            "No ADR sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            []);
        return Task.FromResult(response);
    }

    public Task<Response<TaskPromotionResult>> EnsureSubIssueAsync(string adrIssueNodeId, TaskRecord task, SyncLink? existingTaskLink, bool dryRun = false)
    {
        var response = new Response<TaskPromotionResult>(
            false,
            "No ADR sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            new TaskPromotionResult());
        return Task.FromResult(response);
    }
}
