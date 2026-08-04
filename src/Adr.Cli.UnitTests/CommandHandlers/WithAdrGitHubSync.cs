using Adr.Cli.Services;
using Adr.Cli.Sync;
using Adr.Cli.XLogger;

using McpCore;

using Moq;

using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Tests derived from IAdrGitHubSync's documented contract (ADR 00010) - real AdrSettings/
/// AdrRecordRepository/AdrTasksRepository against an isolated in-memory filesystem, with a mocked
/// IAdrSyncProvider so the GitHub-specific mechanics (already covered by
/// WithGitHubProjectsAdrSyncProvider) aren't re-tested here.
/// </summary>
public sealed class WithAdrGitHubSync
{
    private readonly ITestOutputHelper testOutputHelper;

    public WithAdrGitHubSync(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    private AdrGitHubSync CreateSut(out IAdrRecordRepository adrRepository, out IAdrTasksRepository tasksRepository, Mock<IAdrSyncProvider> providerMock)
    {
        var fileSystem = new MockFileSystem();
        var rootPath = "C:\\repo";
        fileSystem.Directory.CreateDirectory(rootPath);
        fileSystem.Directory.SetCurrentDirectory(rootPath);

        var settings = new AdrSettings(fileSystem);
        var fileLock = new FileLockService(fileSystem, XUnitLogger.CreateLogger<FileLockService>(testOutputHelper));
        var stdOutMock = new Mock<IStdOut>();
        stdOutMock.Setup(m => m.WriteLine(It.IsAny<string>())).Callback<string>(testOutputHelper.WriteLine);

        adrRepository = new AdrRecordRepository(fileSystem, settings, stdOutMock.Object, fileLock, XUnitLogger.CreateLogger<AdrRecordRepository>(testOutputHelper));
        tasksRepository = new AdrTasksRepository(fileSystem, settings, stdOutMock.Object, fileLock, XUnitLogger.CreateLogger<AdrTasksRepository>(testOutputHelper));

        return new AdrGitHubSync(
            settings,
            XUnitLogger.CreateLogger<AdrGitHubSync>(testOutputHelper),
            adrRepository,
            tasksRepository,
            providerMock.Object);
    }

    private const string ProviderName = "GitHubProjects";

    [Fact]
    public async Task ExportAdrAsync_NoIdsOrFilter_FailsWithoutCallingProvider()
    {
        var providerMock = new Mock<IAdrSyncProvider>();
        providerMock.SetupGet(m => m.Name).Returns(ProviderName);
        var sut = CreateSut(out _, out _, providerMock);

        var result = await sut.ExportAdrAsync([], null);

        Assert.False(result.Success);
        providerMock.Verify(m => m.ExportAsync(It.IsAny<AdrRecord>(), It.IsAny<SyncLink?>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ExportAdrAsync_ExplicitId_PersistsReturnedSyncLinkAndCascadesToRelatedTask()
    {
        var providerMock = new Mock<IAdrSyncProvider>();
        providerMock.SetupGet(m => m.Name).Returns(ProviderName);
        providerMock
            .Setup(m => m.ExportAsync(It.IsAny<AdrRecord>(), null, false, false))
            .ReturnsAsync(new Response<AdrExportResult>(true, null, new AdrExportResult
            {
                Created = true,
                ExternalScope = "gjkaal/2",
                ExternalId = "ITEM1",
                ExternalContentType = "Issue",
                IssueNodeId = "ISSUE1",
                SyncState = SyncState.Synced
            }));
        providerMock
            .Setup(m => m.EnsureSubIssueAsync("ISSUE1", It.IsAny<TaskRecord>(), null, false))
            .ReturnsAsync(new Response<TaskPromotionResult>(true, null, new TaskPromotionResult
            {
                Promoted = true,
                ExternalId = "TASK_ITEM1",
                ExternalContentType = "Issue",
                SubIssueLinked = true
            }));

        var sut = CreateSut(out var adrRepository, out var tasksRepository, providerMock);

        await tasksRepository.WriteRecordAsync(new TaskRecord { Title = "Implement provider" });
        var adr = new AdrRecord { Title = "Sync ADRs to GitHub" };
        adr.RelatedTasks[1] = "Implement provider";
        await adrRepository.WriteRecordAsync(adr);

        var result = await sut.ExportAdrAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(SyncItemOutcome.Succeeded, result.Value!.Items[0].Outcome);

        var persistedAdr = await adrRepository.ReadMetadataAsync(1);
        Assert.NotNull(persistedAdr);
        var link = persistedAdr!.SyncLinks.Find(l => l.Provider == ProviderName);
        Assert.NotNull(link);
        Assert.Equal("ITEM1", link!.ExternalId);

        var persistedTask = await tasksRepository.ReadMetadataAsync(1);
        Assert.NotNull(persistedTask);
        var taskLink = persistedTask!.SyncLinks.Find(l => l.Provider == ProviderName);
        Assert.NotNull(taskLink);
        Assert.Equal("TASK_ITEM1", taskLink!.ExternalId);
    }

    [Fact]
    public async Task ExportAdrAsync_PopulatesDecisionAndConsequencesFromMarkdownBeforeExport()
    {
        // AdrRecord.Decision/Consequences are [JsonIgnore] - ReadMetadataAsync alone never returns
        // them. A regression here means every exported ADR silently pushes an empty Decision/
        // Consequences body to GitHub, even though the local .md file has the real content.
        AdrRecord? exportedAdr = null;
        var providerMock = new Mock<IAdrSyncProvider>();
        providerMock.SetupGet(m => m.Name).Returns(ProviderName);
        providerMock
            .Setup(m => m.ExportAsync(It.IsAny<AdrRecord>(), null, false, false))
            .Callback<AdrRecord, SyncLink?, bool, bool>((adr, _, _, _) => exportedAdr = adr)
            .ReturnsAsync(new Response<AdrExportResult>(true, null, new AdrExportResult
            {
                Created = true,
                ExternalScope = "gjkaal/2",
                ExternalId = "ITEM1",
                ExternalContentType = "Issue",
                IssueNodeId = "ISSUE1",
                SyncState = SyncState.Synced
            }));

        var sut = CreateSut(out var adrRepository, out _, providerMock);
        await adrRepository.WriteRecordAsync(new AdrRecord
        {
            Title = "Sync ADRs to GitHub",
            Context = "Some context.",
            Decision = "Some decision.",
            Consequences = "Some consequences."
        });

        var result = await sut.ExportAdrAsync([1], null);

        Assert.True(result.Success);
        Assert.NotNull(exportedAdr);
        // Trim: markdown round-tripping through the template can add trailing whitespace/newlines,
        // which is harmless and expected (the same reason ContentSyncMarker's hash strips whitespace
        // before comparing) - the bug under test is empty content, not exact whitespace fidelity.
        Assert.Equal("Some decision.", exportedAdr!.Decision.Trim());
        Assert.Equal("Some consequences.", exportedAdr.Consequences.Trim());
    }

    [Fact]
    public async Task ExportAdrAsync_ProviderReportsMismatch_MarksLinkAsMismatch()
    {
        var providerMock = new Mock<IAdrSyncProvider>();
        providerMock.SetupGet(m => m.Name).Returns(ProviderName);
        providerMock
            .Setup(m => m.ExportAsync(It.IsAny<AdrRecord>(), It.IsAny<SyncLink?>(), false, false))
            .ReturnsAsync(new Response<AdrExportResult>(true, "mismatch", new AdrExportResult
            {
                ExternalId = "ITEM1",
                ExternalContentType = "Issue",
                SyncState = SyncState.Mismatch
            }));

        var sut = CreateSut(out var adrRepository, out _, providerMock);
        var adr = new AdrRecord { Title = "Sync ADRs to GitHub" };
        adr.SyncLinks.Add(new SyncLink { Provider = ProviderName, ExternalId = "ITEM1" });
        await adrRepository.WriteRecordAsync(adr);

        var result = await sut.ExportAdrAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(SyncItemOutcome.Mismatch, result.Value!.Items[0].Outcome);

        var persisted = await adrRepository.ReadMetadataAsync(1);
        Assert.Equal(SyncState.Mismatch, persisted!.SyncLinks.Find(l => l.Provider == ProviderName)!.SyncState);
    }

    [Fact]
    public async Task ImportAdrStatusAsync_DefaultScope_OnlyIncludesAdrsWithActiveProviderLink()
    {
        var providerMock = new Mock<IAdrSyncProvider>();
        providerMock.SetupGet(m => m.Name).Returns(ProviderName);

        var sut = CreateSut(out var adrRepository, out _, providerMock);

        var linked = new AdrRecord { Title = "Linked ADR" };
        linked.SyncLinks.Add(new SyncLink { Provider = ProviderName, ExternalId = "ITEM1" });
        await adrRepository.WriteRecordAsync(linked);

        await adrRepository.WriteRecordAsync(new AdrRecord { Title = "Unlinked ADR" });

        providerMock
            .Setup(m => m.ImportAsync(It.IsAny<AdrRecord>(), It.IsAny<SyncLink>(), false))
            .ReturnsAsync(new Response<AdrImportResult>(true, null, new AdrImportResult
            {
                ExternalStatusRaw = "Accepted",
                MappedStatus = AdrStatus.Accepted,
                SyncState = SyncState.Synced
            }));

        var result = await sut.ImportAdrStatusAsync([], null);

        Assert.True(result.Success);
        Assert.Single(result.Value!.Items);
        Assert.Equal(1, result.Value.Items[0].RecordId);
    }

    [Fact]
    public async Task ImportAdrStatusAsync_MappedStatus_UpdatesLocalStatus()
    {
        var providerMock = new Mock<IAdrSyncProvider>();
        providerMock.SetupGet(m => m.Name).Returns(ProviderName);

        var sut = CreateSut(out var adrRepository, out _, providerMock);
        var adr = new AdrRecord { Title = "Linked ADR", Status = AdrStatus.Proposed };
        adr.SyncLinks.Add(new SyncLink { Provider = ProviderName, ExternalId = "ITEM1" });
        await adrRepository.WriteRecordAsync(adr);

        providerMock
            .Setup(m => m.ImportAsync(It.IsAny<AdrRecord>(), It.IsAny<SyncLink>(), false))
            .ReturnsAsync(new Response<AdrImportResult>(true, null, new AdrImportResult
            {
                ExternalStatusRaw = "Accepted",
                MappedStatus = AdrStatus.Accepted,
                SyncState = SyncState.Synced
            }));

        var result = await sut.ImportAdrStatusAsync([1], null);

        Assert.True(result.Success);
        Assert.Equal(SyncItemOutcome.Succeeded, result.Value!.Items[0].Outcome);

        var persisted = await adrRepository.ReadMetadataAsync(1);
        Assert.Equal(AdrStatus.Accepted, persisted!.Status);
    }
}
