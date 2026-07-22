using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Adr.Cli.XLogger;

using Microsoft.Extensions.Logging;

using Moq;

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
            new NoOpTaskProposalGenerator());
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
}
