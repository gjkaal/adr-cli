using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Adr.Cli.Sync;
using Adr.Cli.XLogger;

using McpCore;

using Microsoft.Extensions.Logging;

using Moq;

using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Tests derived from IProjectPlanning's documented contract and the task_* MCP tool
/// descriptions - not from reading ProjectPlanning's implementation - covering functionality
/// that previously had no test coverage at all.
/// </summary>
public sealed class WithProjectPlanning
{
    private readonly ITestOutputHelper testOutputHelper;

    public WithProjectPlanning(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Builds a ProjectPlanning instance backed by a real AdrSettings/AdrTasksRepository/
    /// FileLockService against an isolated in-memory filesystem (never the real disk, never this
    /// repo's own adr.config.json), so tests exercise real task creation/lookup/enumeration
    /// instead of hand-mocking IDirectoryInfo enumeration. The repository is returned too, so
    /// tests can inspect real persisted state (Status, DueDate, Logs, Related) directly through
    /// the documented IAdrTasksRepository contract instead of parsing JSON/file paths by hand.
    /// </summary>
    private ProjectPlanning CreateSut(out IAdrTasksRepository repository)
    {
        return CreateSut(out repository, new NoOpTaskSyncProvider());
    }

    private ProjectPlanning CreateSut(out IAdrTasksRepository repository, ITaskSyncProvider syncProvider)
    {
        var fileSystem = new MockFileSystem();
        var rootPath = "C:\\repo";
        fileSystem.Directory.CreateDirectory(rootPath);
        fileSystem.Directory.SetCurrentDirectory(rootPath);

        var settings = new AdrSettings(fileSystem);
        var fileLock = new FileLockService(fileSystem, XUnitLogger.CreateLogger<FileLockService>(testOutputHelper));
        var stdOutMock = new Mock<IStdOut>();
        stdOutMock.Setup(m => m.WriteLine(It.IsAny<string>())).Callback<string>(testOutputHelper.WriteLine);
        var processHelperMock = new Mock<IProcessHelper>();

        repository = new AdrTasksRepository(
            fileSystem,
            settings,
            stdOutMock.Object,
            fileLock,
            XUnitLogger.CreateLogger<AdrTasksRepository>(testOutputHelper));

        return new ProjectPlanning(
            settings,
            XUnitLogger.CreateLogger<AdrNew>(testOutputHelper),
            repository,
            stdOutMock.Object,
            processHelperMock.Object,
            new NoOpTaskProposalGenerator(),
            syncProvider);
    }

    [Fact]
    public async Task NewTaskAsync_CreatedTask_StartsWithStatusNew()
    {
        var sut = CreateSut(out var repository);

        var result = await sut.NewTaskAsync("Set up CI pipeline", "Wire up build and test stages", null, false);
        Assert.True(result.Success);

        var record = await repository.ReadMetadataAsync(1);
        Assert.NotNull(record);
        Assert.Equal(PlanningStatus.New, record!.Status);
    }

    [Fact]
    public async Task NewTaskAsync_WithNoDueDate_LeavesDueDateUnset()
    {
        // task_new's MCP description: "Omit if there is no due date" - and TaskRecord.DueDate is
        // a nullable DateTime, so omitting it should leave it null rather than some sentinel date.
        var sut = CreateSut(out var repository);

        await sut.NewTaskAsync("Task without a due date", "No deadline", null, false);

        var record = await repository.ReadMetadataAsync(1);
        Assert.NotNull(record);
        Assert.Null(record!.DueDate);
    }

    [Fact]
    public async Task NewTaskAsync_WithDueDate_ParsesTheDate()
    {
        var sut = CreateSut(out var repository);

        await sut.NewTaskAsync("Ship the release", "Cut and publish", "2026-08-01", false);

        var record = await repository.ReadMetadataAsync(1);
        Assert.NotNull(record);
        Assert.Equal(new System.DateTime(2026, 8, 1), record!.DueDate);
    }

    [Fact]
    public async Task FindTasksAsync_OnlyReturnsTasksMatchingStatus_WhenStatusIsSpecified()
    {
        // IProjectPlanning.FindTasksAsync: "Only include tasks with the requested status,
        // ignored for status=None."
        var sut = CreateSut(out _);

        await sut.NewTaskAsync("Widget rollout phase one", "First phase", null, false);
        await sut.NewTaskAsync("Widget rollout phase two", "Second phase", null, false);

        // Move only the first task to Active; the second stays at its initial "New" status.
        await sut.UpdateTaskAsync("1", PlanningStatus.Active, "Started work");

        var activeOnly = await sut.FindTasksAsync("Widget", PlanningStatus.Active, false, false, false);

        Assert.Contains("phase one", activeOnly.Message);
        Assert.DoesNotContain("phase two", activeOnly.Message);
    }

    [Fact]
    public async Task FindTasksAsync_ReturnsTasksInAnyStatus_WhenStatusIsNone()
    {
        var sut = CreateSut(out _);

        await sut.NewTaskAsync("Gadget rollout phase one", "First phase", null, false);
        await sut.NewTaskAsync("Gadget rollout phase two", "Second phase", null, false);
        await sut.UpdateTaskAsync("1", PlanningStatus.Completed, "Done");

        var all = await sut.FindTasksAsync("Gadget", PlanningStatus.None, false, false, false);

        Assert.Contains("phase one", all.Message);
        Assert.Contains("phase two", all.Message);
    }

    [Fact]
    public async Task FindTasksAsync_MatchesAnySingleWord_CaseInsensitively()
    {
        // task_find's MCP description: OR-across-words, case-insensitive substring match.
        var sut = CreateSut(out _);

        await sut.NewTaskAsync("Upgrade database schema", "Migrate to new column layout", null, false);

        var result = await sut.FindTasksAsync("zzz-no-match DATABASE", PlanningStatus.None, false, false, false);

        Assert.Contains("Upgrade database schema", result.Message);
    }

    [Fact]
    public async Task FindTasksAsync_DoesNotReturnUnrelatedTasks()
    {
        var sut = CreateSut(out _);

        await sut.NewTaskAsync("Completely unrelated task", "Nothing to do with the query", null, false);

        var result = await sut.FindTasksAsync("zzz-does-not-exist", PlanningStatus.None, false, false, false);

        Assert.DoesNotContain("Completely unrelated task", result.Message);
    }

    [Fact]
    public async Task UpdateTaskAsync_ChangesStatus_AndKeepsFullHistory()
    {
        // task_update's MCP description: "appends a justification entry to its status log (the
        // log is kept, not overwritten - every status change is retained for history)".
        var sut = CreateSut(out var repository);
        await sut.NewTaskAsync("Investigate flaky test", "Root-cause the failure", null, false);

        var first = await sut.UpdateTaskAsync("1", PlanningStatus.Active, "Started investigating");
        Assert.True(first.Success);

        var second = await sut.UpdateTaskAsync("1", PlanningStatus.Completed, "Root cause fixed");
        Assert.True(second.Success);

        var record = await repository.ReadMetadataAsync(1);
        Assert.NotNull(record);
        Assert.Equal(PlanningStatus.Completed, record!.Status);
        Assert.Equal(2, record.Logs.Count);
        Assert.Equal("Started investigating", record.Logs[0].Justification);
        Assert.Equal("Root cause fixed", record.Logs[1].Justification);
    }

    [Fact]
    public async Task UpdateTaskAsync_Fails_WhenTaskIdDoesNotExist()
    {
        var sut = CreateSut(out _);

        var result = await sut.UpdateTaskAsync("999", PlanningStatus.Active, "No such task");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RemoveTaskLinkAsync_IsOneDirectional_ReverseLinkSurvives()
    {
        // IProjectPlanning.RemoveTaskLinkAsync: "Remove all links from the source to the target
        // (reverse links are not removed)."
        var sut = CreateSut(out var repository);
        await sut.NewTaskAsync("Task A", "First task", null, false);
        await sut.NewTaskAsync("Task B", "Second task", null, false);

        var linkAtoB = await sut.LinkTaskAsync(1, 2, "blocks");
        Assert.True(linkAtoB.Success);
        var linkBtoA = await sut.LinkTaskAsync(2, 1, "blocked by");
        Assert.True(linkBtoA.Success);

        var removeAtoB = await sut.RemoveTaskLinkAsync(1, 2);
        Assert.True(removeAtoB.Success);

        var taskA = await repository.ReadMetadataAsync(1);
        var taskB = await repository.ReadMetadataAsync(2);

        Assert.NotNull(taskA);
        Assert.NotNull(taskB);

        // A's forward link to B is gone...
        Assert.DoesNotContain(2, taskA!.Related.Keys);
        // ...but B's reverse link back to A must remain untouched.
        Assert.Contains(1, taskB!.Related.Keys);
    }

    [Fact]
    public async Task ExportTasksAsync_NoIdsOrFilter_FailsWithoutCallingProvider()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out _, provider);

        var result = await sut.ExportTasksAsync([], null);

        Assert.False(result.Success);
        Assert.Empty(provider.ExportedTaskIds);
    }

    [Fact]
    public async Task ExportTasksAsync_ExplicitId_PersistsReturnedSyncLink()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);

        var result = await sut.ExportTasksAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.SucceededCount);

        var record = await repository.ReadMetadataAsync(1);
        var link = Assert.Single(record!.SyncLinks);
        Assert.Equal(FakeTaskSyncProvider.Name, link.Provider);
        Assert.Equal("ITEM-1", link.ExternalId);
        Assert.Equal(SyncState.Synced, link.SyncState);
    }

    [Fact]
    public async Task ExportTasksAsync_ProviderFailure_DoesNotPersistSyncMetadata()
    {
        var provider = new FakeTaskSyncProvider { FailExport = true };
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);

        var result = await sut.ExportTasksAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.FailedCount);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Empty(record!.SyncLinks);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_DefaultScope_OnlyIncludesTasksWithActiveProviderLink()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Has a link", "Exported already", null, false);
        await sut.NewTaskAsync("No link", "Never exported", null, false);
        await sut.ExportTasksAsync([1], null);

        var result = await sut.ImportTaskStatusAsync([], null);

        Assert.True(result.Success);
        Assert.Single(result.Value!.Items);
        Assert.Equal(1, result.Value.Items[0].RecordId);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_MappedStatus_UpdatesLocalStatusAndLogsJustification()
    {
        var provider = new FakeTaskSyncProvider { ImportedStatus = PlanningStatus.Active };
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);
        await sut.ExportTasksAsync([1], null);

        var result = await sut.ImportTaskStatusAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.SucceededCount);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Equal(PlanningStatus.Active, record!.Status);
        Assert.Contains(record.Logs, log => log.Justification.Contains(FakeTaskSyncProvider.Name));
    }

    [Fact]
    public async Task ImportTaskStatusAsync_UnmappedExternalStatus_LeavesLocalStatusUnchanged()
    {
        var provider = new FakeTaskSyncProvider { ImportedStatus = null };
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);
        await sut.ExportTasksAsync([1], null);

        var result = await sut.ImportTaskStatusAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.UnmappedCount);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Equal(PlanningStatus.New, record!.Status);
    }

    [Fact]
    public async Task ExportTasksAsync_ProviderReportsMismatch_ReportsMismatchAndDoesNotRefreshLastSyncedAt()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);
        await sut.ExportTasksAsync([1], null);
        var beforeMismatch = (await repository.ReadMetadataAsync(1))!.SyncLinks[0].LastSyncedAt;

        provider.ExportMismatch = true;
        var result = await sut.ExportTasksAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.MismatchCount);

        var record = await repository.ReadMetadataAsync(1);
        var link = record!.SyncLinks[0];
        Assert.Equal(SyncState.Mismatch, link.SyncState);
        Assert.Equal(beforeMismatch, link.LastSyncedAt);
    }

    [Fact]
    public async Task ExportTasksAsync_Force_OverwritesDespiteMismatch()
    {
        var provider = new FakeTaskSyncProvider { ExportMismatch = true };
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);

        var result = await sut.ExportTasksAsync([1], null, force: true);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.SucceededCount);
        Assert.True(provider.ExportForceFlags[^1]);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Equal(SyncState.Synced, record!.SyncLinks[0].SyncState);
    }

    [Fact]
    public async Task ExportTasksAsync_DryRun_ReportsOutcomeWithoutPersistingSyncLink()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);

        var result = await sut.ExportTasksAsync([1], null, dryRun: true);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.SucceededCount);
        Assert.True(provider.ExportDryRunFlags[^1]);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Empty(record!.SyncLinks);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_DryRun_ReportsStatusWithoutPersistingOrAdopting()
    {
        var provider = new FakeTaskSyncProvider { ImportedStatus = PlanningStatus.Active };
        provider.ItemsToDiscover.Add(new DiscoveredExternalItem
        {
            ExternalScope = "fake/scope",
            ExternalId = "ITEM-99",
            Title = "Created directly on the board",
            Body = "Description from the board.",
            HasMarker = false
        });
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);
        await sut.ExportTasksAsync([1], null);

        var result = await sut.ImportTaskStatusAsync([], null, dryRun: true);

        Assert.True(result.Success);
        // Linked task #1 (would set Active) and the discovered item (would be adopted) both report,
        // but dry-run creates no file for the latter - RecordId 0 signals nothing was actually made.
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.All(result.Value.Items, i => Assert.Equal(SyncItemOutcome.Succeeded, i.Outcome));
        Assert.Contains(result.Value.Items, i => i.RecordId == 0 && (i.Message?.Contains("[DRY RUN]") ?? false));

        var record = await repository.ReadMetadataAsync(1);
        Assert.Equal(PlanningStatus.New, record!.Status);
        Assert.Empty(record.Logs);
        var files = repository.ReadMetadataAsync(2);
        Assert.Null(await files);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_ProviderReportsMismatch_LeavesLocalTaskUntouched()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);
        await sut.ExportTasksAsync([1], null);

        provider.ImportMismatch = true;
        var result = await sut.ImportTaskStatusAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.MismatchCount);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Equal(PlanningStatus.New, record!.Status);
        Assert.Equal("Automate build and test", record.Description);
        Assert.Equal(SyncState.Mismatch, record.SyncLinks[0].SyncState);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_SafePull_AppliesRemoteContentAndPushesRefreshedMarker()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Wire up CI", "Automate build and test", null, false);
        await sut.ExportTasksAsync([1], null);

        provider.PulledContent = ("Wire up CI (renamed)", "Someone edited this on GitHub.");
        var exportCallsBeforeImport = provider.ExportedTaskIds.Count;
        var result = await sut.ImportTaskStatusAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.SucceededCount);
        // The import triggered a re-export to refresh the hash marker after adopting remote content.
        Assert.Equal(exportCallsBeforeImport + 1, provider.ExportedTaskIds.Count);

        var record = await repository.ReadMetadataAsync(1);
        Assert.Equal("Wire up CI (renamed)", record!.Title);
        Assert.Equal("Someone edited this on GitHub.", record.Description);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_DefaultScope_AdoptsUnmatchedDiscoveredItemAsNewLocalTask()
    {
        var provider = new FakeTaskSyncProvider();
        provider.ItemsToDiscover.Add(new DiscoveredExternalItem
        {
            ExternalScope = "fake/scope",
            ExternalId = "ITEM-99",
            Title = "Created directly on the board",
            Body = "Description from the board.",
            HasMarker = false
        });
        var sut = CreateSut(out var repository, provider);

        var result = await sut.ImportTaskStatusAsync([], null);

        Assert.True(result.Success);
        Assert.Single(result.Value!.Items);
        var adopted = result.Value.Items[0];
        Assert.Equal(SyncItemOutcome.Succeeded, adopted.Outcome);

        var record = await repository.ReadMetadataAsync(adopted.RecordId);
        Assert.NotNull(record);
        Assert.Equal("Created directly on the board", record!.Title);
        Assert.Equal("Description from the board.", record.Description);
        Assert.Single(record.SyncLinks);
        Assert.Equal("ITEM-99", record.SyncLinks[0].ExternalId);
    }

    [Fact]
    public async Task ImportTaskStatusAsync_DefaultScope_SkipsDiscoveredItemMatchingExistingLocalTitle()
    {
        var provider = new FakeTaskSyncProvider();
        var sut = CreateSut(out var repository, provider);
        await sut.NewTaskAsync("Already local", "Existing task", null, false);

        provider.ItemsToDiscover.Add(new DiscoveredExternalItem
        {
            ExternalScope = "fake/scope",
            ExternalId = "ITEM-99",
            Title = "already local",
            Body = "Should not create a duplicate.",
            HasMarker = false
        });

        var result = await sut.ImportTaskStatusAsync([], null);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Items);

        var files = repository.ReadMetadataAsync(2);
        Assert.Null(await files);
    }

    /// <summary>
    /// A minimal, in-memory ITaskSyncProvider double - not a real connector - used to exercise
    /// ProjectPlanning's export/import orchestration (selection, persistence, batch reporting)
    /// without a network call. Provider-internal behavior (GraphQL, field mapping, etc.) is covered
    /// separately in Adr.Cli.Sync.GitHubProjects.UnitTests.
    /// </summary>
    private sealed class FakeTaskSyncProvider : ITaskSyncProvider
    {
        public const string Name = "Fake";
        string ITaskSyncProvider.Name => Name;

        public bool FailExport { get; set; }
        public bool ExportMismatch { get; set; }
        public PlanningStatus? ImportedStatus { get; set; } = PlanningStatus.Active;
        public bool ImportMismatch { get; set; }
        public (string Title, string Body)? PulledContent { get; set; }
        public List<int> ExportedTaskIds { get; } = new();
        public List<bool> ExportForceFlags { get; } = new();
        public List<bool> ExportDryRunFlags { get; } = new();

        public Task<Response<TaskExportResult>> ExportAsync(TaskRecord task, SyncLink? existingLink, bool force = false, bool dryRun = false)
        {
            ExportedTaskIds.Add(task.RecordId);
            ExportForceFlags.Add(force);
            ExportDryRunFlags.Add(dryRun);
            if (FailExport)
            {
                return Task.FromResult(new Response<TaskExportResult>(false, "Simulated export failure.", new TaskExportResult()));
            }

            if (ExportMismatch && !force)
            {
                var mismatchResult = new TaskExportResult
                {
                    Created = false,
                    ExternalScope = "fake/scope",
                    ExternalId = existingLink?.ExternalId ?? $"ITEM-{task.RecordId}",
                    SyncState = SyncState.Mismatch
                };
                return Task.FromResult(new Response<TaskExportResult>(true, "Simulated export mismatch.", mismatchResult));
            }

            var result = new TaskExportResult
            {
                Created = existingLink == null,
                ExternalScope = "fake/scope",
                ExternalId = dryRun && existingLink == null ? string.Empty : (!string.IsNullOrWhiteSpace(existingLink?.ExternalId) ? existingLink.ExternalId : $"ITEM-{task.RecordId}"),
                ExternalUrl = $"https://example.invalid/items/{task.RecordId}",
                SyncState = SyncState.Synced
            };
            return Task.FromResult(new Response<TaskExportResult>(true, dryRun ? "[DRY RUN] Simulated." : null, result));
        }

        public Task<Response<TaskImportResult>> ImportAsync(TaskRecord task, SyncLink existingLink, bool dryRun = false)
        {
            if (ImportMismatch)
            {
                return Task.FromResult(new Response<TaskImportResult>(true, "Simulated import mismatch.", new TaskImportResult { SyncState = SyncState.Mismatch }));
            }

            var result = new TaskImportResult
            {
                MappedStatus = ImportedStatus,
                ExternalStatusRaw = ImportedStatus?.ToString() ?? "Unmapped-External-Value",
                SyncState = ImportedStatus.HasValue ? SyncState.Synced : SyncState.Unmapped
            };
            if (PulledContent.HasValue)
            {
                result.PulledTitle = PulledContent.Value.Title;
                result.PulledBody = PulledContent.Value.Body;
            }
            return Task.FromResult(new Response<TaskImportResult>(true, null, result));
        }

        public List<DiscoveredExternalItem> ItemsToDiscover { get; } = new();

        public Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync()
        {
            return Task.FromResult(new Response<IReadOnlyList<DiscoveredExternalItem>>(true, null, ItemsToDiscover));
        }
    }
}
