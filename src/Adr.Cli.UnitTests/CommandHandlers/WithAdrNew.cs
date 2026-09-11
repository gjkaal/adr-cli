using Adr.Cli.Ai;
using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Adr.Cli.XLogger;

using Microsoft.Extensions.Logging;

using Moq;

using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Adr.Cli.CommandHandlers;

public sealed class WithAdrNew
{
    private readonly Mock<IStdOut> stdOutMock = new();
    private readonly Mock<IProcessHelper> processHelperMock = new();
    private readonly Mock<IAdrLink> linkMock = new();
    private readonly Mock<IAdrProposalGenerator> proposalGeneratorMock = new();
    private readonly ITestOutputHelper testOutputHelper;
    private readonly ILogger<AdrNew> logger;

    public WithAdrNew(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
        logger = XUnitLogger.CreateLogger<AdrNew>(testOutputHelper);
    }

    private AdrNew CreateSut(MockFileSystem fileSystem, out AdrSettings settings, out AdrRecordRepository repository)
    {
        settings = new AdrSettings(fileSystem);
        var fileLock = new FileLockService(fileSystem, XUnitLogger.CreateLogger<FileLockService>(testOutputHelper));
        repository = new AdrRecordRepository(fileSystem, settings, stdOutMock.Object, fileLock, XUnitLogger.CreateLogger<AdrRecordRepository>(testOutputHelper));

        return new AdrNew(
            settings,
            logger,
            repository,
            stdOutMock.Object,
            processHelperMock.Object,
            linkMock.Object,
            proposalGeneratorMock.Object);
    }

    private static MockFileSystem CreateRepoFileSystem(string rootPath)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(rootPath);
        fileSystem.Directory.SetCurrentDirectory(rootPath);
        return fileSystem;
    }

    [Fact]
    public async Task UpdateContentAsync_DecisionOnly_ReplacesDecisionAndLeavesConsequencesUntouched()
    {
        var fileSystem = CreateRepoFileSystem(@"C:\repo");
        var sut = CreateSut(fileSystem, out var settings, out var repository);

        var record = new AdrRecord
        {
            Title = "Use a message bus",
            Status = AdrStatus.Proposed,
            Context = "Original context.",
            Decision = "Original decision.",
            Consequences = "Original consequences.",
            TemplateType = TemplateType.Ad
        };
        await repository.WriteRecordAsync(record);

        var result = await sut.UpdateContentAsync(record.RecordId, "New decision text.", null);

        Assert.True(result.Success, result.Message);
        var content = await repository.ReadContentAsync(record.RecordId);
        var markdown = string.Join('\n', content);
        Assert.Contains("New decision text.", markdown);
        Assert.Contains("Original consequences.", markdown);
        Assert.DoesNotContain("Original decision.", markdown);
    }

    [Fact]
    public async Task UpdateContentAsync_ConsequencesOnly_ReplacesConsequencesAndLeavesDecisionUntouched()
    {
        var fileSystem = CreateRepoFileSystem(@"C:\repo");
        var sut = CreateSut(fileSystem, out var settings, out var repository);

        var record = new AdrRecord
        {
            Title = "Use a message bus",
            Status = AdrStatus.Proposed,
            Context = "Original context.",
            Decision = "Original decision.",
            Consequences = "Original consequences.",
            TemplateType = TemplateType.Ad
        };
        await repository.WriteRecordAsync(record);

        var result = await sut.UpdateContentAsync(record.RecordId, null, "*Pro's:*\n- a\n\n*Con's:*\n- b");

        Assert.True(result.Success, result.Message);
        var content = await repository.ReadContentAsync(record.RecordId);
        var markdown = string.Join('\n', content);
        Assert.Contains("Original decision.", markdown);
        Assert.Contains("*Pro's:*", markdown);
        Assert.DoesNotContain("Original consequences.", markdown);
    }

    [Fact]
    public async Task UpdateContentAsync_BothFields_ReplacesBothAndLeavesStatusContextUntouched()
    {
        var fileSystem = CreateRepoFileSystem(@"C:\repo");
        var sut = CreateSut(fileSystem, out var settings, out var repository);

        var record = new AdrRecord
        {
            Title = "Use a message bus",
            Status = AdrStatus.Proposed,
            Context = "Original context.",
            Decision = "Original decision.",
            Consequences = "Original consequences.",
            TemplateType = TemplateType.Ad
        };
        await repository.WriteRecordAsync(record);

        var result = await sut.UpdateContentAsync(record.RecordId, "New decision.", "New consequences.");

        Assert.True(result.Success, result.Message);
        var content = await repository.ReadContentAsync(record.RecordId);
        var markdown = string.Join('\n', content);
        Assert.Contains("New decision.", markdown);
        Assert.Contains("New consequences.", markdown);
        Assert.Contains("Original context.", markdown);
        Assert.Contains("__Proposed__", markdown);

        // Decision/Consequences are markdown-only (JsonIgnore) - metadata must be unaffected.
        var metadata = await repository.ReadMetadataAsync(record.RecordId);
        Assert.Equal(AdrStatus.Proposed, metadata!.Status);
        Assert.Equal("Original context.", metadata.Context);
    }

    [Fact]
    public async Task NewAdrAsync_CreatesDecisionRecord_MarkdownAndMetadataStatusAgree()
    {
        var fileSystem = CreateRepoFileSystem(@"C:\repo");
        var sut = CreateSut(fileSystem, out var settings, out var repository);

        // RepositoryInitialized() requires at least one existing .md, mirroring what adr_init leaves behind.
        await repository.WriteRecordAsync(new AdrRecord { Title = "Use ADRs", Status = AdrStatus.Accepted, TemplateType = TemplateType.Ad });

        var result = await sut.NewAdrAsync("Use a message bus", false, "0", "", false);
        Assert.True(result.Success, result.Message);

        var newestFile = settings.DocFolderInfo().EnumerateFiles("*.md")
            .OrderByDescending(f => int.Parse(f.Name.Split('-')[0]))
            .First();
        var recordId = int.Parse(newestFile.Name.Split('-')[0]);

        // The .json must not silently drop Status just because it equals AdrStatus.New - that was
        // the exact defect (New used to be the enum's zero value, and WhenWritingDefault omits
        // properties equal to their type's CLR default).
        var jsonFile = settings.GetMetaFile(DocumentType.Adr, newestFile.Name.Replace(".md", string.Empty));
        var jsonContent = await jsonFile.OpenText().ReadToEndAsync();
        Assert.Contains("\"Status\"", jsonContent);

        var metadata = await repository.ReadMetadataAsync(recordId);
        Assert.Equal(AdrStatus.New, metadata!.Status);

        var content = await repository.ReadContentAsync(recordId);
        var markdown = string.Join('\n', content);
        Assert.Contains("__New__", markdown);

        var query = new AdrQuery(settings, XUnitLogger.CreateLogger<AdrNew>(testOutputHelper), repository, stdOutMock.Object);
        var listResult = await query.ListAdrAsync(false, false);
        Assert.Contains("New", listResult.Message);
    }

    [Fact]
    public async Task UpdateContentAsync_NeitherFieldProvided_FailsWithoutTouchingTheFile()
    {
        var fileSystem = CreateRepoFileSystem(@"C:\repo");
        var sut = CreateSut(fileSystem, out var settings, out var repository);

        var record = new AdrRecord { Title = "Use a message bus", TemplateType = TemplateType.Ad };
        await repository.WriteRecordAsync(record);

        var result = await sut.UpdateContentAsync(record.RecordId, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateContentAsync_RecordDoesNotExist_FailsCleanly()
    {
        var fileSystem = CreateRepoFileSystem(@"C:\repo");
        var sut = CreateSut(fileSystem, out var settings, out var repository);

        var result = await sut.UpdateContentAsync(999, "New decision.", null);

        Assert.False(result.Success);
    }
}
