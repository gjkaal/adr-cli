using System.Collections.Generic;
using System.Threading.Tasks;

using Adr.Cli.Sync;

using McpCore;

namespace Adr.Cli.Services;

/// <summary>
/// Default <see cref="ITaskSyncProvider" /> registered when no connector is configured in
/// adr.config.json. Always fails, so <c>task-export</c>/<c>task-import</c> callers never need a
/// null check.
/// </summary>
public class NoOpTaskSyncProvider : ITaskSyncProvider
{
    public string Name => string.Empty;

    public Task<Response<TaskExportResult>> ExportAsync(TaskRecord task, SyncLink? existingLink, bool force = false, bool dryRun = false)
    {
        var response = new Response<TaskExportResult>(
            false,
            "No task sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            new TaskExportResult());
        return Task.FromResult(response);
    }

    public Task<Response<TaskImportResult>> ImportAsync(TaskRecord task, SyncLink existingLink, bool dryRun = false)
    {
        var response = new Response<TaskImportResult>(
            false,
            "No task sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            new TaskImportResult());
        return Task.FromResult(response);
    }

    public Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync()
    {
        var response = new Response<IReadOnlyList<DiscoveredExternalItem>>(
            false,
            "No task sync provider is configured. Add a \"sync\" section to adr.config.json to enable it.",
            []);
        return Task.FromResult(response);
    }
}
