using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Adr.Cli.Ai;
using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Adr.Cli.Sync;

using McpCore;

using Microsoft.Extensions.Logging;

namespace Adr.Cli.CommandHandlers;

public class ProjectPlanning : IProjectPlanning
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrNew> logger;
    private readonly IAdrTasksRepository repository;
    private readonly IStdOut stdOut;
    private readonly IProcessHelper processHelper;
    private readonly ITaskProposalGenerator proposalGenerator;
    private readonly ITaskSyncProvider syncProvider;

    public ProjectPlanning(
        IAdrSettings settings,
        ILogger<AdrNew> logger,
        IAdrTasksRepository repository,
        IStdOut stdOut,
        IProcessHelper processHelper,
        ITaskProposalGenerator proposalGenerator,
        ITaskSyncProvider syncProvider)
    {
        this.settings = settings;
        this.logger = logger;
        this.repository = repository;
        this.stdOut = stdOut;
        this.processHelper = processHelper;
        this.proposalGenerator = proposalGenerator;
        this.syncProvider = syncProvider;
    }

    public async Task<Response> NewTaskAsync(string title, string description, string? dueDate, bool useAi)
    {
        if (!settings.TasksInitialized())
        {
            return Response.Fail($"Tasks folder is not initialized {settings.TasksFolderInfo().FullName}.");
        }

        Response result;
        logger.LogInformation($"Creating new tasks record.");
        result = await CreateTaskAsync(title, description, dueDate, useAi);
        return result;
    }

    private async Task<Response> CreateTaskAsync(string title, string description, string? dueDate, bool useAi)
    {
        DateTime? parsedDueDate = null;
        if (!string.IsNullOrEmpty(dueDate))
        {
            if (DateTime.TryParse(dueDate, out var date))
            {
                parsedDueDate = date;
            }
            else
            {
                logger.LogWarning("Could not get due date from {StringValue}", dueDate);
            }
        }

        var record = new TaskRecord
        {
            Title = title,
            Status = PlanningStatus.New,
            Description = description,
            DueDate = parsedDueDate
        };

        await ApplyAiProposalAsync(record, useAi);
        await repository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"Task is created in {settings.TasksFolder}.");
    }

    /// <summary>
    /// Draft Description/Details for <paramref name="record" /> using the configured AI provider. A
    /// no-op when <paramref name="useAi" /> is false. On any AI failure, logs a warning and leaves
    /// the record exactly as it was - the task is still created from the template. A user-supplied
    /// Description is preserved rather than overwritten by the AI's draft.
    /// </summary>
    private async Task ApplyAiProposalAsync(TaskRecord record, bool useAi)
    {
        if (!useAi)
        {
            return;
        }

        var hadUserSuppliedDescription = !string.IsNullOrEmpty(record.Description);
        var existingTasks = await GetExistingTaskSummariesAsync();
        var result = await proposalGenerator.GenerateAsync(record.Title, record.Description, existingTasks, TemplateType.Task.ToString());
        if (!result.Success || result.Value == null)
        {
            logger.LogWarning("AI proposal generation failed, continuing without it: {Message}", result.Message);
            stdOut.WriteLine($"AI proposal generation failed, continuing without it: {result.Message}");
            return;
        }

        if (!hadUserSuppliedDescription && !string.IsNullOrEmpty(result.Value.Description))
        {
            record.Description = result.Value.Description;
        }
        record.Details = result.Value.Details;
    }

    private async Task<IReadOnlyList<TaskSummary>> GetExistingTaskSummariesAsync()
    {
        var summaries = new List<TaskSummary>();
        foreach (var file in settings.TasksFolderInfo().EnumerateFiles("*.md"))
        {
            var separatorIndex = file.Name.IndexOf('-');
            if (separatorIndex <= 0 || !int.TryParse(file.Name[..separatorIndex], out var recordId))
            {
                continue;
            }

            var record = await repository.ReadMetadataAsync(recordId);
            if (record == null)
            {
                continue;
            }

            summaries.Add(new TaskSummary
            {
                RecordId = record.RecordId,
                Title = record.Title,
                Status = record.Status,
                Description = record.Description
            });
        }

        return summaries;
    }

    public async Task<Response> FindTasksAsync(string filter, PlanningStatus status, bool sortReverse, bool verbose, bool includeContent)
    {
        logger.LogDebug("Find tasks containing '{Filter}' {SortOrder}", filter, sortReverse ? "newest first" : "oldest first");

        var sb = new StringBuilder();
        var listMeta = new Dictionary<int, string>();
        var idList = FindRecordIds(0);
        var words = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            sb.AppendLine("-- No filter provided --");
        }

        var items = idList.Distinct();
        if (sortReverse)
        {
            items = items.Reverse();
        }

        foreach (var recordId in items)
        {
            var showRecord = false;
            var adr = await repository.ReadMetadataAsync(recordId);
            if (adr == null)
            {
                continue;
            }

            if (status != PlanningStatus.None && adr.Status != status)
            {
                continue;
            }

            foreach (var word in words)
            {
                if (adr.Title.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    showRecord = true;
                    break;
                }
                if (!showRecord && adr.Description.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    showRecord = true;
                    break;
                }
                if (!showRecord && includeContent)
                {
                    var content = await repository.ReadContentAsync(recordId);
                    if (content.Any(m => m.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    {
                        showRecord = true;
                        break;
                    }
                }
            }

            if (showRecord)
            {
                var information = verbose
                    ? adr.VerboseString()
                    : adr.FormatString();
                listMeta.Add(adr.RecordId, information);
            }
        }

        foreach (var recordId in listMeta.Keys)
        {
            sb.AppendLine(listMeta[recordId]);
        }
        return Response.Ok(sb.ToString());
    }

    public async Task<Response> GeneratePlanningTocAsync()
    {
        var toc = new StringBuilder();
        var projectName = settings.ProjectName;

        // Add file description
        toc.AppendLine($"# {projectName}");
        toc.AppendLine();
        toc.AppendLine($"__Date__ : {DateTime.Now:F}");
        toc.AppendLine();
        toc.AppendLine("This file contains the table of contents for the open tasks.");
        toc.AppendLine("It is auto generated by the adr-cli tool, any manual modifications are overwritten.");
        toc.AppendLine();

        // Add table header
        toc.AppendLine("# Current open tasks:");
        toc.AppendLine();
        toc.AppendLine("| Adr | Title | Status | Due date |");
        toc.AppendLine("| --- | ----- | ------ | -------- |");

        // Add table content
        var docFolder = settings.TasksFolderInfo();
        foreach (var docInfo in docFolder.EnumerateFiles("*.md").OrderBy(x => x.Name))
        {
            var recordIdPart = docInfo.Name.Split('-')[0];
            if (int.TryParse(recordIdPart, out var recordId))
            {
                var record = await repository.ReadMetadataAsync(recordId);
                if (record == null)
                {
                    continue;
                }

                // only relevant tasks
                if (Constants.InactiveStatusList.Contains(record.Status))
                {
                    continue;
                }

                // tasks-toc.md is written next to docFolder's parent (see CreateRootDocumentAsync), so
                // the link only needs docFolder's own name, not a path back up to the repo root.
                var link = $"{docFolder.Name}/{record.FileName}.md";
                toc.AppendLine($"| {record.RecordId} | [{record.Title}]({link}) | {record.Status} | {record.DueDate} |");
            }
        }
        toc.AppendLine();

        var (success, generatedFile) = await repository.CreateRootDocumentAsync("tasks-toc.md", toc);

        return success
            ? Response.Ok($"Generated Tasks overview in {generatedFile}.")
            : Response.Fail($"Generating Tasks overview in {generatedFile} is not completed.");
    }

    public async Task<Response> ListTasksAsync(bool sortReverse, bool verbose)
    {
        logger.LogDebug("List ADR {SortOrder}", sortReverse ? "newest first" : "oldest first");

        var listMeta = new Dictionary<int, string>();
        var idList = FindRecordIds(0);

        var items = idList.Distinct();
        if (sortReverse)
        {
            items = items.Reverse();
        }

        foreach (var recordId in items)
        {
            var adr = await repository.ReadMetadataAsync(recordId);
            if (adr == null)
            {
                continue;
            }

            var information = verbose
                ? adr.VerboseString()
                : adr.FormatString();
            listMeta.Add(adr.RecordId, information);
        }

        var sb = new StringBuilder();
        foreach (var recordId in listMeta.Keys)
        {
            sb.AppendLine(listMeta[recordId]);
        }
        return Response.Ok(sb.ToString());
    }

    private List<int> FindRecordIds(int startFromRecord)
    {
        var dir = settings.TasksFolderInfo();
        var metadataFiles = dir.EnumerateFiles("*.md").Select(m => m.Name);
        var idList = new List<int>();
        foreach (var metadataFile in metadataFiles)
        {
            var parts = metadataFile.Split('-');
            if (parts.Length < 1)
            {
                continue;
            }

            if (int.TryParse(parts[0], out var recordId) && recordId >= startFromRecord)
            {
                idList.Add(recordId);
            }
        }

        return idList;
    }

    public async Task<Response> LinkTaskAsync(int sourceId, int targetId, string remark)
    {
        var source = await repository.ReadMetadataAsync(sourceId);
        if (source == null)
        {
            return Response.Fail($"Could not find source record with id {sourceId}");
        }

        var target = await repository.ReadMetadataAsync(targetId);
        if (target == null)
        {
            return Response.Fail($"Could not find target record with id {targetId}");
        }

        if (source.Related.ContainsKey(targetId))
        {
            return Response.Fail($"Task '{source.Title}' is already related to '{target.Title}'.");
        }
        source.Related[targetId] = target.Title;
        source.Logs.Add(new StatusUpdate { DateTime = DateTime.UtcNow, Status = PlanningStatus.Related, Justification = remark });

        var updateCount = await repository.UpdateMetadataAsync(sourceId, source);

        return updateCount < 0
            ? Response.Fail($"Could not update task '{source.Title}' with Id:{sourceId}")
            : updateCount > 0 ? Response.Ok($"Task '{source.Title}' with Id:{sourceId} is now linked to task '{target.Title}' with Id:{targetId}")
            : Response.Fail($"Task '{source.Title}' with Id:{sourceId} is not modified.");
    }

    public async Task<Response> RemoveTaskLinkAsync(int sourceId, int targetId)
    {
        var source = await repository.ReadMetadataAsync(sourceId);
        if (source == null)
        {
            return Response.Fail($"Could not find source record with id {sourceId}");
        }

        if (!source.Related.ContainsKey(targetId))
        {
            return Response.Fail($"Task '{source.Title}' is not related to a task with Id:{targetId}.");
        }
        source.Related.Remove(targetId);

        var updateCount = await repository.UpdateMetadataAsync(sourceId, source);
        return updateCount < 0
            ? Response.Fail($"Could not update task '{source.Title}' with Id:{sourceId}")
            : updateCount > 0 ? Response.Ok($"Task '{source.Title}' with Id:{sourceId} is modified, the relation to task with Id:{targetId} is removed.")
            : Response.Fail($"Task '{source.Title}' with Id:{sourceId} is not modified.");
    }

    public async Task<Response> UpdateTaskAsync(string sourceId, PlanningStatus status, string justification)
    {
        if (!int.TryParse(sourceId, out var id))
        {
            return Response.Fail($"Could not get valid record id from {sourceId}");
        }
        var record = await repository.ReadMetadataAsync(id);
        if (record == null)
        {
            return Response.Fail($"Could not find record with id {sourceId}");
        }
        record.Status = status;
        record.Logs.Add(new StatusUpdate { DateTime = DateTime.UtcNow, Status = status, Justification = justification });
        var updateCount = await repository.UpdateMetadataAsync(id, record);

        return updateCount < 0
            ? Response.Fail($"Could not update task '{record.Title}' with Id:{id} current status is {record.Status}")
            : updateCount > 0 ? Response.Ok($"Task '{record.Title}' with Id:{id} has a new status: {record.Status}")
            : Response.Fail($"No status update for task '{record.Title}' with Id:{id} current status is {record.Status}");
    }

    public async Task<Response<TaskSyncBatchResult>> ExportTasksAsync(IReadOnlyList<int> taskIds, string? filter, bool force = false, bool dryRun = false)
    {
        if (taskIds.Count == 0 && string.IsNullOrWhiteSpace(filter))
        {
            return new Response<TaskSyncBatchResult>(false, "Specify at least one task id (--id) or a filter (-q) to export.", new TaskSyncBatchResult());
        }

        if (force && taskIds.Count != 1)
        {
            logger.LogWarning("task-export --force used with {Count} tasks selected - intended for a single task at a time.", taskIds.Count == 0 ? "a filter matching multiple" : taskIds.Count.ToString());
        }

        var tasks = await ResolveTaskSelectionAsync(taskIds, filter);
        var batch = new TaskSyncBatchResult();

        foreach (var task in tasks)
        {
            batch.Items.Add(await ExportOneAsync(task, force, dryRun));
        }

        return new Response<TaskSyncBatchResult>(true, null, batch);
    }

    private async Task<TaskSyncItemResult> ExportOneAsync(TaskRecord task, bool force = false, bool dryRun = false)
    {
        var item = new TaskSyncItemResult { RecordId = task.RecordId, Title = task.Title };
        var existingLink = task.SyncLinks.Find(link => link.Provider == syncProvider.Name);

        var response = await syncProvider.ExportAsync(task, existingLink, force, dryRun);
        if (!response.Success || response.Value == null)
        {
            item.Outcome = TaskSyncItemOutcome.Failed;
            item.Message = response.Message;
            return item;
        }

        var result = response.Value;

        if (result.SyncState == TaskSyncState.Mismatch)
        {
            // Mismatch only ever comes from the update path (an existing link with a divergent
            // remote), so existingLink is always set here - a brand-new export has no baseline to
            // check and never returns Mismatch.
            if (!dryRun)
            {
                existingLink!.SyncState = TaskSyncState.Mismatch;
                existingLink.ExternalContentType = result.ExternalContentType;
                await repository.UpdateMetadataAsync(task.RecordId, task);
            }
            item.Outcome = TaskSyncItemOutcome.Mismatch;
            item.Message = response.Message ?? "Export refused: content mismatch.";
            item.ExternalId = result.ExternalId;
            return item;
        }

        item.Outcome = result.SyncState == TaskSyncState.Synced ? TaskSyncItemOutcome.Succeeded : TaskSyncItemOutcome.Unmapped;
        item.Message = result.SyncState == TaskSyncState.Synced
            ? (result.Created ? "Created." : "Updated.")
            : "Local status has no entry in the export status map; external item content was still created/updated.";
        item.ExternalId = result.ExternalId;
        item.ExternalUrl = result.ExternalUrl;

        if (dryRun)
        {
            item.Message = response.Message ?? item.Message;
            return item;
        }

        if (existingLink == null)
        {
            existingLink = new TaskSyncLink { Provider = syncProvider.Name };
            task.SyncLinks.Add(existingLink);
        }
        existingLink.ExternalScope = result.ExternalScope;
        existingLink.ExternalId = result.ExternalId;
        existingLink.ExternalContentType = result.ExternalContentType;
        existingLink.ExternalUrl = result.ExternalUrl;
        existingLink.SyncState = result.SyncState;
        existingLink.LastSyncedAt = DateTime.UtcNow;
        await repository.UpdateMetadataAsync(task.RecordId, task);

        return item;
    }

    public async Task<Response<TaskSyncBatchResult>> ImportTaskStatusAsync(IReadOnlyList<int> taskIds, string? filter, bool dryRun = false)
    {
        var batch = new TaskSyncBatchResult();

        if (taskIds.Count > 0 || !string.IsNullOrWhiteSpace(filter))
        {
            foreach (var task in await ResolveTaskSelectionAsync(taskIds, filter))
            {
                batch.Items.Add(await ImportOneAsync(task, dryRun));
            }
            return new Response<TaskSyncBatchResult>(true, null, batch);
        }

        // Default scope: every task already linked to the active provider, plus discovering and
        // adopting board items that have no local counterpart yet (see ADR 00009).
        foreach (var task in await ResolveTasksWithActiveProviderLinkAsync())
        {
            batch.Items.Add(await ImportOneAsync(task, dryRun));
        }
        batch.Items.AddRange(await DiscoverAndAdoptNewTasksAsync(dryRun));

        return new Response<TaskSyncBatchResult>(true, null, batch);
    }

    /// <summary>
    /// Finds items on the active provider's board with no content-sync marker (never pushed through
    /// this tool) and no matching local task title, and adopts each as a brand-new local task -
    /// linked, pushed once to establish a valid hash marker, and its current status imported so it
    /// doesn't sit at the default "New" status until a second <c>task-import</c> run.
    /// </summary>
    private async Task<List<TaskSyncItemResult>> DiscoverAndAdoptNewTasksAsync(bool dryRun = false)
    {
        var results = new List<TaskSyncItemResult>();
        var discoverResponse = await syncProvider.DiscoverItemsAsync();
        if (!discoverResponse.Success || discoverResponse.Value == null)
        {
            // Best-effort: discovery not supported/configured is not a batch failure.
            return results;
        }

        var localTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in FindRecordIds(0).Distinct())
        {
            var record = await repository.ReadMetadataAsync(id);
            if (record != null)
            {
                localTitles.Add(record.Title.Trim());
            }
        }

        foreach (var discovered in discoverResponse.Value)
        {
            if (discovered.HasMarker || localTitles.Contains(discovered.Title.Trim()))
            {
                continue;
            }

            results.Add(await AdoptDiscoveredItemAsync(discovered, dryRun));
        }

        return results;
    }

    private async Task<TaskSyncItemResult> AdoptDiscoveredItemAsync(DiscoveredExternalItem discovered, bool dryRun = false)
    {
        var (description, details) = TaskBodyFormat.Split(discovered.Body);
        var task = new TaskRecord
        {
            Title = discovered.Title,
            Status = PlanningStatus.New,
            Description = description,
            Details = details
        };

        if (dryRun)
        {
            return new TaskSyncItemResult
            {
                RecordId = 0,
                Title = task.Title,
                Outcome = TaskSyncItemOutcome.Succeeded,
                Message = $"[DRY RUN] Would adopt as a new local task from {syncProvider.Name} (external id {discovered.ExternalId}). No file was created.",
                ExternalId = discovered.ExternalId
            };
        }

        var link = new TaskSyncLink
        {
            Provider = syncProvider.Name,
            ExternalScope = discovered.ExternalScope,
            ExternalId = discovered.ExternalId
        };
        task.SyncLinks.Add(link);

        await repository.WriteRecordAsync(task);
        var item = new TaskSyncItemResult { RecordId = task.RecordId, Title = task.Title };

        // Push back immediately to embed a valid hash marker, per ADR 00009 - the item currently has
        // none, so nothing can safely detect divergence against it yet.
        var exportResponse = await syncProvider.ExportAsync(task, link);
        if (!exportResponse.Success || exportResponse.Value == null)
        {
            await repository.UpdateMetadataAsync(task.RecordId, task);
            item.Outcome = TaskSyncItemOutcome.Failed;
            item.Message = $"Adopted as new local task #{task.RecordId} from {syncProvider.Name}, but pushing a hash marker failed: {exportResponse.Message}";
            item.ExternalId = discovered.ExternalId;
            return item;
        }

        link.ExternalScope = exportResponse.Value.ExternalScope;
        link.ExternalId = exportResponse.Value.ExternalId;
        link.ExternalContentType = exportResponse.Value.ExternalContentType;
        link.ExternalUrl = exportResponse.Value.ExternalUrl;
        link.SyncState = exportResponse.Value.SyncState;
        link.LastSyncedAt = DateTime.UtcNow;
        await repository.UpdateMetadataAsync(task.RecordId, task);

        // Also pull current status right away, so the new task doesn't sit at "New" until a second
        // task-import run.
        var statusResult = await ImportOneAsync(task);

        item.Outcome = TaskSyncItemOutcome.Succeeded;
        item.Message = $"Adopted as new local task #{task.RecordId} from {syncProvider.Name}. {statusResult.Message}";
        item.ExternalId = link.ExternalId;
        item.ExternalUrl = link.ExternalUrl;
        return item;
    }

    private async Task<TaskSyncItemResult> ImportOneAsync(TaskRecord task, bool dryRun = false)
    {
        var item = new TaskSyncItemResult { RecordId = task.RecordId, Title = task.Title };
        var existingLink = task.SyncLinks.Find(link => link.Provider == syncProvider.Name);
        if (existingLink == null || string.IsNullOrWhiteSpace(existingLink.ExternalId))
        {
            item.Outcome = TaskSyncItemOutcome.Skipped;
            item.Message = "No sync link for the currently active provider.";
            return item;
        }

        var response = await syncProvider.ImportAsync(task, existingLink, dryRun);
        if (!response.Success || response.Value == null)
        {
            item.Outcome = TaskSyncItemOutcome.Failed;
            item.Message = response.Message;
            return item;
        }

        var result = response.Value;

        if (result.SyncState == TaskSyncState.Mismatch)
        {
            // Content diverged on both sides - neither local nor remote is touched. Status may still
            // have been read by the provider in principle, but we treat a content mismatch as
            // blocking the whole import, so status is left alone here too.
            if (!dryRun)
            {
                existingLink.ExternalContentType = result.ExternalContentType;
                existingLink.SyncState = TaskSyncState.Mismatch;
                await repository.UpdateMetadataAsync(task.RecordId, task);
            }
            item.Outcome = TaskSyncItemOutcome.Mismatch;
            item.Message = response.Message ?? "Import skipped: content mismatch.";
            item.ExternalId = existingLink.ExternalId;
            return item;
        }

        var contentPulled = result.PulledTitle != null && result.PulledBody != null;

        string statusMessage;
        if (result.SyncState == TaskSyncState.Synced && result.MappedStatus.HasValue)
        {
            item.Outcome = TaskSyncItemOutcome.Succeeded;
            statusMessage = $"Status set to {result.MappedStatus.Value}.";
        }
        else
        {
            item.Outcome = TaskSyncItemOutcome.Unmapped;
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
            task.Title = result.PulledTitle!;
            var (description, details) = TaskBodyFormat.Split(result.PulledBody!);
            task.Description = description;
            task.Details = details;
        }

        if (result.SyncState == TaskSyncState.Synced && result.MappedStatus.HasValue)
        {
            task.Status = result.MappedStatus.Value;
            task.Logs.Add(new StatusUpdate
            {
                DateTime = DateTime.UtcNow,
                Status = result.MappedStatus.Value,
                Justification = $"Imported from {syncProvider.Name} (external status: {result.ExternalStatusRaw})."
            });
        }

        existingLink.SyncState = result.SyncState;
        existingLink.LastSyncedAt = DateTime.UtcNow;
        await repository.UpdateMetadataAsync(task.RecordId, task);

        if (!contentPulled)
        {
            item.Message = statusMessage;
            return item;
        }

        // Local adopted remote's content - push again to refresh the hash marker baseline (see ADR
        // 00009), so the next sync in either direction compares against the content we just adopted,
        // not the stale pre-pull baseline.
        var pushBack = await syncProvider.ExportAsync(task, existingLink);
        if (pushBack.Success && pushBack.Value != null && pushBack.Value.SyncState != TaskSyncState.Mismatch)
        {
            existingLink.ExternalScope = pushBack.Value.ExternalScope;
            existingLink.ExternalId = pushBack.Value.ExternalId;
            existingLink.ExternalContentType = pushBack.Value.ExternalContentType;
            existingLink.ExternalUrl = pushBack.Value.ExternalUrl;
            existingLink.LastSyncedAt = DateTime.UtcNow;
            await repository.UpdateMetadataAsync(task.RecordId, task);
            item.Message = $"Pulled updated content from {syncProvider.Name}. {statusMessage}";
        }
        else
        {
            item.Message = $"Pulled updated content from {syncProvider.Name}, but refreshing the remote hash marker failed: {pushBack.Message ?? "unknown error"}. {statusMessage}";
        }

        return item;
    }

    private async Task<List<TaskRecord>> ResolveTaskSelectionAsync(IReadOnlyList<int> taskIds, string? filter)
    {
        var records = new List<TaskRecord>();

        if (taskIds.Count > 0)
        {
            foreach (var id in taskIds.Distinct())
            {
                var record = await repository.ReadMetadataAsync(id);
                if (record != null)
                {
                    records.Add(record);
                }
            }
            return records;
        }

        var words = (filter ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var id in FindRecordIds(0).Distinct())
        {
            var record = await repository.ReadMetadataAsync(id);
            if (record == null)
            {
                continue;
            }

            if (words.Any(word => record.Title.Contains(word, StringComparison.OrdinalIgnoreCase) || record.Description.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                records.Add(record);
            }
        }
        return records;
    }

    private async Task<List<TaskRecord>> ResolveTasksWithActiveProviderLinkAsync()
    {
        var records = new List<TaskRecord>();
        foreach (var id in FindRecordIds(0).Distinct())
        {
            var record = await repository.ReadMetadataAsync(id);
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
}