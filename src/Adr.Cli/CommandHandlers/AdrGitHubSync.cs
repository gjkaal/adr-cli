using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Adr.Cli.Extensions;
using Adr.Cli.Sync;

using McpCore;

using Microsoft.Extensions.Logging;

namespace Adr.Cli.CommandHandlers;

public class AdrGitHubSync : IAdrGitHubSync
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrGitHubSync> logger;
    private readonly IAdrRecordRepository adrRepository;
    private readonly IAdrTasksRepository tasksRepository;
    private readonly IAdrSyncProvider syncProvider;

    public AdrGitHubSync(
        IAdrSettings settings,
        ILogger<AdrGitHubSync> logger,
        IAdrRecordRepository adrRepository,
        IAdrTasksRepository tasksRepository,
        IAdrSyncProvider syncProvider)
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRepository = adrRepository;
        this.tasksRepository = tasksRepository;
        this.syncProvider = syncProvider;
    }

    public async Task<Response<SyncBatchResult>> ExportAdrAsync(IReadOnlyList<int> adrIds, string? filter, bool force = false, bool dryRun = false)
    {
        if (adrIds.Count == 0 && string.IsNullOrWhiteSpace(filter))
        {
            return new Response<SyncBatchResult>(false, "Specify at least one ADR id (--id) or a filter (-q) to export.", new SyncBatchResult());
        }

        if (force && adrIds.Count != 1)
        {
            logger.LogWarning("adr-export --force used with {Count} ADRs selected - intended for a single ADR at a time.", adrIds.Count == 0 ? "a filter matching multiple" : adrIds.Count.ToString());
        }

        var adrs = await ResolveAdrSelectionAsync(adrIds, filter);
        var batch = new SyncBatchResult();

        foreach (var adr in adrs)
        {
            batch.Items.Add(await ExportOneAsync(adr, force, dryRun));
        }

        return new Response<SyncBatchResult>(true, null, batch);
    }

    private async Task<SyncItemResult> ExportOneAsync(AdrRecord adr, bool force, bool dryRun)
    {
        var item = new SyncItemResult { RecordId = adr.RecordId, Title = adr.Title };
        var existingLink = adr.SyncLinks.Find(link => link.Provider == syncProvider.Name);

        var response = await syncProvider.ExportAsync(adr, existingLink, force, dryRun);
        if (!response.Success || response.Value == null)
        {
            item.Outcome = SyncItemOutcome.Failed;
            item.Message = response.Message;
            return item;
        }

        var result = response.Value;

        if (result.SyncState == SyncState.Mismatch)
        {
            if (!dryRun)
            {
                existingLink!.SyncState = SyncState.Mismatch;
                existingLink.ExternalContentType = result.ExternalContentType;
                await adrRepository.UpdateMetadataAsync(adr.RecordId, adr);
            }
            item.Outcome = SyncItemOutcome.Mismatch;
            item.Message = response.Message ?? "Export refused: content mismatch.";
            item.ExternalId = result.ExternalId;
            return item;
        }

        item.Outcome = result.SyncState == SyncState.Synced ? SyncItemOutcome.Succeeded : SyncItemOutcome.Unmapped;
        item.Message = result.SyncState == SyncState.Synced
            ? (result.Created ? "Created." : "Updated.")
            : "Local status has no entry in the export status map; external issue content was still created/updated.";
        item.ExternalId = result.ExternalId;
        item.ExternalUrl = result.ExternalUrl;

        if (dryRun)
        {
            item.Message = response.Message ?? item.Message;
            return item;
        }

        if (existingLink == null)
        {
            existingLink = new SyncLink { Provider = syncProvider.Name };
            adr.SyncLinks.Add(existingLink);
        }
        existingLink.ExternalScope = result.ExternalScope;
        existingLink.ExternalId = result.ExternalId;
        existingLink.ExternalContentType = result.ExternalContentType;
        existingLink.ExternalUrl = result.ExternalUrl;
        existingLink.SyncState = result.SyncState;
        existingLink.LastSyncedAt = DateTime.UtcNow;
        await adrRepository.UpdateMetadataAsync(adr.RecordId, adr);

        if (result.IssueNodeId != null)
        {
            await AttachRelatedTasksAsync(adr, result.IssueNodeId, dryRun);
        }

        return item;
    }

    /// <summary>
    /// Cascades an ADR export into promoting/attaching each related task as a GitHub sub-issue - see
    /// ADR 00010. One task failing to attach is logged and skipped; it does not fail the ADR's own
    /// export, which has already succeeded by the time this runs.
    /// </summary>
    private async Task AttachRelatedTasksAsync(AdrRecord adr, string adrIssueNodeId, bool dryRun)
    {
        foreach (var taskId in adr.RelatedTasks.Keys)
        {
            var task = await tasksRepository.ReadMetadataAsync(taskId);
            if (task == null)
            {
                continue;
            }

            var taskLink = task.SyncLinks.Find(link => link.Provider == syncProvider.Name);
            var promotionResponse = await syncProvider.EnsureSubIssueAsync(adrIssueNodeId, task, taskLink, dryRun);
            if (!promotionResponse.Success || promotionResponse.Value == null)
            {
                logger.LogWarning("adr-export: failed to attach task {TaskId} as a sub-issue of ADR {AdrId}: {Message}", taskId, adr.RecordId, promotionResponse.Message);
                continue;
            }

            if (dryRun)
            {
                continue;
            }

            var promotion = promotionResponse.Value;
            if (taskLink == null)
            {
                taskLink = new SyncLink { Provider = syncProvider.Name };
                task.SyncLinks.Add(taskLink);
            }
            taskLink.ExternalScope = adr.SyncLinks.Find(link => link.Provider == syncProvider.Name)?.ExternalScope ?? taskLink.ExternalScope;
            taskLink.ExternalId = promotion.ExternalId;
            taskLink.ExternalContentType = promotion.ExternalContentType;
            taskLink.ExternalUrl = promotion.ExternalUrl;
            taskLink.LastSyncedAt = DateTime.UtcNow;
            await tasksRepository.UpdateMetadataAsync(taskId, task);
        }
    }

    public async Task<Response<SyncBatchResult>> ImportAdrStatusAsync(IReadOnlyList<int> adrIds, string? filter, bool dryRun = false)
    {
        var batch = new SyncBatchResult();

        if (adrIds.Count > 0 || !string.IsNullOrWhiteSpace(filter))
        {
            foreach (var adr in await ResolveAdrSelectionAsync(adrIds, filter))
            {
                batch.Items.Add(await ImportOneAsync(adr, dryRun));
            }
            return new Response<SyncBatchResult>(true, null, batch);
        }

        // Default scope: every ADR already linked to the active provider. Unlike task-import, there
        // is no discovery/adoption of board-only items as brand-new local ADRs - creating a new ADR
        // from a bare external issue would need Decision/Consequences content this ADR doesn't
        // provide a path for (see ADR 00010).
        foreach (var adr in await ResolveAdrsWithActiveProviderLinkAsync())
        {
            batch.Items.Add(await ImportOneAsync(adr, dryRun));
        }

        return new Response<SyncBatchResult>(true, null, batch);
    }

    private async Task<SyncItemResult> ImportOneAsync(AdrRecord adr, bool dryRun = false)
    {
        var item = new SyncItemResult { RecordId = adr.RecordId, Title = adr.Title };
        var existingLink = adr.SyncLinks.Find(link => link.Provider == syncProvider.Name);
        if (existingLink == null || string.IsNullOrWhiteSpace(existingLink.ExternalId))
        {
            item.Outcome = SyncItemOutcome.Skipped;
            item.Message = "No sync link for the currently active provider.";
            return item;
        }

        var response = await syncProvider.ImportAsync(adr, existingLink, dryRun);
        if (!response.Success || response.Value == null)
        {
            item.Outcome = SyncItemOutcome.Failed;
            item.Message = response.Message;
            return item;
        }

        var result = response.Value;

        if (result.SyncState == SyncState.Mismatch)
        {
            if (!dryRun)
            {
                existingLink.ExternalContentType = result.ExternalContentType;
                existingLink.SyncState = SyncState.Mismatch;
                await adrRepository.UpdateMetadataAsync(adr.RecordId, adr);
            }
            item.Outcome = SyncItemOutcome.Mismatch;
            item.Message = response.Message ?? "Import skipped: content mismatch.";
            item.ExternalId = existingLink.ExternalId;
            return item;
        }

        var contentPulled = result.PulledTitle != null && result.PulledBody != null;

        string statusMessage;
        if (result.SyncState == SyncState.Synced && result.MappedStatus.HasValue)
        {
            item.Outcome = SyncItemOutcome.Succeeded;
            statusMessage = $"Status set to {result.MappedStatus.Value}.";
        }
        else
        {
            item.Outcome = SyncItemOutcome.Unmapped;
            statusMessage = $"External status \"{result.ExternalStatusRaw}\" has no import mapping; local status left unchanged.";
        }

        item.ExternalId = existingLink.ExternalId;

        if (dryRun)
        {
            item.Message = contentPulled
                ? $"[DRY RUN] Would pull updated content from {syncProvider.Name} and refresh its hash marker. {statusMessage}"
                : $"[DRY RUN] {statusMessage}";
            return item;
        }

        existingLink.ExternalContentType = result.ExternalContentType;
        if (!string.IsNullOrEmpty(result.ExternalUrl))
        {
            existingLink.ExternalUrl = result.ExternalUrl;
        }

        if (contentPulled)
        {
            var (context, decision, consequences) = AdrBodyFormat.Split(result.PulledBody!);
            var contentLines = await adrRepository.ReadContentAsync(adr.RecordId);
            var newContent = contentLines
                .ReplaceMdContent("Context", context.Split(Environment.NewLine))
                .ToArray();
            newContent = newContent
                .ReplaceMdContent("Decision", decision.Split(Environment.NewLine))
                .ToArray();
            newContent = newContent
                .ReplaceMdContent("Consequences", consequences.Split(Environment.NewLine))
                .ToArray();

            adr.Title = result.PulledTitle!;
            adr.Context = context;
            adr.Decision = decision;
            adr.Consequences = consequences;

            await adrRepository.UpdateContentAsync(adr, newContent);
        }

        if (result.SyncState == SyncState.Synced && result.MappedStatus.HasValue)
        {
            adr.Status = result.MappedStatus.Value;
        }

        existingLink.SyncState = result.SyncState;
        existingLink.LastSyncedAt = DateTime.UtcNow;
        await adrRepository.UpdateMetadataAsync(adr.RecordId, adr);

        if (!contentPulled)
        {
            item.Message = statusMessage;
            return item;
        }

        // Local adopted remote's content - push again to refresh the hash marker baseline, per the
        // same rationale ADR 00009 gives for tasks.
        var pushBack = await syncProvider.ExportAsync(adr, existingLink);
        if (pushBack.Success && pushBack.Value != null && pushBack.Value.SyncState != SyncState.Mismatch)
        {
            existingLink.ExternalScope = pushBack.Value.ExternalScope;
            existingLink.ExternalId = pushBack.Value.ExternalId;
            existingLink.ExternalContentType = pushBack.Value.ExternalContentType;
            existingLink.ExternalUrl = pushBack.Value.ExternalUrl;
            existingLink.LastSyncedAt = DateTime.UtcNow;
            await adrRepository.UpdateMetadataAsync(adr.RecordId, adr);
            item.Message = $"Pulled updated content from {syncProvider.Name}. {statusMessage}";
        }
        else
        {
            item.Message = $"Pulled updated content from {syncProvider.Name}, but refreshing the remote hash marker failed: {pushBack.Message ?? "unknown error"}. {statusMessage}";
        }

        return item;
    }

    private async Task<List<AdrRecord>> ResolveAdrSelectionAsync(IReadOnlyList<int> adrIds, string? filter)
    {
        var records = new List<AdrRecord>();

        if (adrIds.Count > 0)
        {
            foreach (var id in adrIds.Distinct())
            {
                var record = await LoadFullAdrAsync(id);
                if (record != null)
                {
                    records.Add(record);
                }
            }
            return records;
        }

        var words = (filter ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var id in FindRecordIds())
        {
            var record = await LoadFullAdrAsync(id);
            if (record == null)
            {
                continue;
            }

            if (words.Any(word => record.Title.Contains(word, StringComparison.OrdinalIgnoreCase) || record.Context.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                records.Add(record);
            }
        }
        return records;
    }

    private async Task<List<AdrRecord>> ResolveAdrsWithActiveProviderLinkAsync()
    {
        var records = new List<AdrRecord>();
        foreach (var id in FindRecordIds())
        {
            var record = await LoadFullAdrAsync(id);
            if (record == null)
            {
                continue;
            }

            if (record.SyncLinks.Exists(link => link.Provider == syncProvider.Name && !string.IsNullOrWhiteSpace(link.ExternalId)))
            {
                records.Add(record);
            }
        }
        return records;
    }

    /// <summary>
    /// Reads an ADR's metadata and populates <see cref="AdrRecord.Decision" />/
    /// <see cref="AdrRecord.Consequences" /> from its markdown content - both are <c>[JsonIgnore]</c>
    /// and therefore always empty on whatever <see cref="IAdrRecordRepository.ReadMetadataAsync" />
    /// returns on its own. Every ADR loaded for export/import content sync must go through this, not
    /// a bare ReadMetadataAsync, or the pushed/compared body silently loses its Decision/Consequences.
    /// </summary>
    private async Task<AdrRecord?> LoadFullAdrAsync(int id)
    {
        var record = await adrRepository.ReadMetadataAsync(id);
        if (record == null)
        {
            return null;
        }

        var content = await adrRepository.ReadContentAsync(id);
        record.UpdateFromMarkdown(id, content, out _);
        return record;
    }

    private List<int> FindRecordIds()
    {
        var dir = settings.DocFolderInfo();
        var metadataFiles = dir.EnumerateFiles("*.md").Select(m => m.Name);
        var idList = new List<int>();
        foreach (var metadataFile in metadataFiles)
        {
            var parts = metadataFile.Split('-');
            if (parts.Length < 1)
            {
                continue;
            }

            if (int.TryParse(parts[0], out var recordId))
            {
                idList.Add(recordId);
            }
        }

        return idList.Distinct().ToList();
    }
}
