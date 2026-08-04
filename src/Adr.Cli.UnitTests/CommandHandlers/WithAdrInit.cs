using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Adr.Cli.Sync;
using Adr.Cli.XLogger;

using Microsoft.Extensions.Logging;

using Moq;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Adr.Cli.CommandHandlers;

public sealed class WithAdrInit
{
    private readonly Mock<IAdrSettings> settingsMock = new();
    private readonly Mock<IAdrRecordRepository> repositoryMock = new();
    private readonly Mock<IProcessHelper> procesMock = new();
    private readonly Mock<IFileInfo> contentFileMock = new();
    private readonly Mock<IStdOut> stdOutMock = new();
    private readonly ITestOutputHelper testOutputHelper;
    private readonly ILogger<AdrInit> logger;

    // see https://www.meziantou.net/how-to-get-asp-net-core-logs-in-the-output-of-xunit-tests.htm
    // for information about xunit ilogger interception

    public WithAdrInit(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
        logger = XUnitLogger.CreateLogger<AdrInit>(testOutputHelper);
        stdOutMock.Setup(m => m.WriteLine(It.IsAny<string>())).Callback<string>(s => testOutputHelper.WriteLine(s));
    }

    [Fact]
    public void AdrInitCommandHandler_CanInitialize()
    {
        IAdrInit sut = new AdrInit(
            new Mock<IAdrSettings>().Object,
            logger,
            new Mock<IAdrRecordRepository>().Object,
            new Mock<IStdOut>().Object,
            new Mock<IProcessHelper>().Object
            );
        Assert.NotNull(sut);
        testOutputHelper.WriteLine("AdrInit completed");
    }

    [Fact]
    public async Task AdrInitCommandHandler_ExecuteInitialize_Async()
    {
        settingsMock.SetupGet(m => m.DefaultDocFolder).Returns("\\adrInit\\tests\\docs");
        settingsMock.SetupGet(m => m.DefaultTemplates).Returns("\\adrInit\\tests\\templates");
        settingsMock.SetupGet(m => m.DefaultTasksFolder).Returns("\\adrInit\\tests\\tasks");
        settingsMock.Setup(m => m.GetContentFile(It.IsAny<DocumentType>(), It.IsAny<string>())).Returns(contentFileMock.Object);
        settingsMock.SetupGet(m => m.DocFolderInfo().FullName).Returns("testFolder");
        settingsMock.SetupGet(m => m.AiSettings).Returns(new AiProviderSettings());
        contentFileMock.SetupGet(m => m.Exists).Returns(true);
        AdrInit sut = new AdrInit(settingsMock.Object, logger, repositoryMock.Object, stdOutMock.Object, procesMock.Object);
        var result = await sut.InitializeAsync("doc", "template", "planning");
        Assert.True(result.Success);
    }

    /// <summary>
    /// Regression test for a real incident: an external process (a stale build of this tool, running
    /// as a long-lived MCP server) re-derived an ADR's metadata from its markdown and silently wiped
    /// SyncLinks/RelatedTasks/References in the process, because its own AdrRecord type predated
    /// those fields - any unknown JSON properties were dropped on deserialize, then never
    /// re-serialized. `sync`/`adr_sync` must only ever touch the fields it actually derives from
    /// markdown (Title, Status, Context, Decision, Consequences) and must leave every other field on
    /// the record exactly as it was, no matter what generated the JSON that's currently on disk.
    /// </summary>
    [Fact]
    public async Task SyncMetadataAsync_OnlyUpdatesMarkdownDerivedFields_LeavesSyncLinksRelatedTasksAndReferencesUntouched()
    {
        var fileSystem = new MockFileSystem();
        var rootPath = "C:\\repo";
        fileSystem.Directory.CreateDirectory(rootPath);
        fileSystem.Directory.SetCurrentDirectory(rootPath);

        var settings = new AdrSettings(fileSystem);
        var fileLock = new FileLockService(fileSystem, XUnitLogger.CreateLogger<FileLockService>(testOutputHelper));
        var repository = new AdrRecordRepository(fileSystem, settings, stdOutMock.Object, fileLock, XUnitLogger.CreateLogger<AdrRecordRepository>(testOutputHelper));

        var record = new AdrRecord
        {
            Title = "Sync should preserve non-markdown fields",
            Status = AdrStatus.Proposed,
            Context = "Original context.",
            TemplateType = TemplateType.Ad
        };
        record.References[2] = "Extends";
        record.RelatedTasks[5] = "Implements this";
        record.SyncLinks.Add(new SyncLink
        {
            Provider = "GitHubProjects",
            ExternalScope = "gjkaal/2",
            ExternalId = "ITEM1",
            ExternalContentType = "Issue",
            ExternalUrl = "https://github.com/gjkaal/adr-cli/issues/7",
            SyncState = SyncState.Synced
        });
        await repository.WriteRecordAsync(record);

        // Simulate someone hand-editing the markdown outside adr-cli, e.g. via a text editor.
        var content = await repository.ReadContentAsync(record.RecordId);
        var edited = content.ReplaceMdContent("Status", ["__Accepted__"]).ToArray();
        edited = edited.ReplaceMdContent("Context", ["Updated context from markdown."]).ToArray();
        await repository.UpdateContentAsync(record, edited);

        var sut = new AdrInit(settings, logger, repository, stdOutMock.Object, procesMock.Object);
        var result = await sut.SyncMetadataAsync(startFromRecordId: 1, onlyForRecordId: record.RecordId);
        Assert.True(result.Success);

        var afterSync = await repository.ReadMetadataAsync(record.RecordId);
        Assert.NotNull(afterSync);

        // Fields sync is actually supposed to derive from markdown - these should reflect the edit.
        Assert.Equal(AdrStatus.Accepted, afterSync!.Status);
        Assert.Equal("Updated context from markdown.", afterSync.Context);

        // Fields sync has no business touching - these must survive unchanged.
        Assert.Equal(record.References, afterSync.References);
        Assert.Equal(record.RelatedTasks, afterSync.RelatedTasks);
        Assert.Single(afterSync.SyncLinks);
        Assert.Equal("ITEM1", afterSync.SyncLinks[0].ExternalId);
        Assert.Equal("https://github.com/gjkaal/adr-cli/issues/7", afterSync.SyncLinks[0].ExternalUrl);
        Assert.Equal(SyncState.Synced, afterSync.SyncLinks[0].SyncState);
    }
}