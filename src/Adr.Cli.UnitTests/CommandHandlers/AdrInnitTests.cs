using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Adr.Cli.XLogger;
using Xunit;
using Xunit.Abstractions;

namespace Adr.Cli.CommandHandlers;
public class AdrInitTests
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

    public AdrInitTests(ITestOutputHelper testOutputHelper)
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
        settingsMock.Setup(m => m.GetContentFile(It.IsAny<string>())).Returns(contentFileMock.Object);
        settingsMock.SetupGet(m => m.DocFolderInfo().FullName).Returns("testFolder");
        contentFileMock.SetupGet(m => m.Exists).Returns(true);
        IAdrInit sut = new AdrInit(settingsMock.Object, logger, repositoryMock.Object, stdOutMock.Object, procesMock.Object);
        var result = await sut.InitializeAsync("doc", "template");
        Assert.Equal(0, result);
    }
}
