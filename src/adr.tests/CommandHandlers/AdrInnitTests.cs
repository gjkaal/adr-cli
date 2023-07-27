using adr;
using adr.CommandHandlers;
using adr.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Tests.XLogger;
using Xunit;
using Xunit.Abstractions;

namespace Tests.CommandHandlers;
public class AdrInitTests
{
    private readonly Mock<IAdrSettings> settingsMock = new();
    private readonly Mock<IAdrRecordRepository> repositoryMock = new();
    private readonly Mock<IProcessHelper> procesMock = new();
    private readonly Mock<IFileInfo> contentFileMock = new();
    private readonly ITestOutputHelper testOutputHelper;
    private readonly ILogger<AdrInit> logger;

    // see https://www.meziantou.net/how-to-get-asp-net-core-logs-in-the-output-of-xunit-tests.htm
    // for information about xunit ilogger interception

    public AdrInitTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
        logger = XUnitLogger.CreateLogger<AdrInit>(testOutputHelper);
    }

    [Fact]
    public void AdrInitCommandHandler_CanInitialize()
    {
        IAdrInit sut = new AdrInit(
            new Mock<IAdrSettings>().Object, 
            logger, 
            new Mock<IAdrRecordRepository>().Object,
            new Mock<IProcessHelper>().Object
            );
        Assert.NotNull(sut);
    }

    [Fact]
    public async Task AdrInitCommandHandler_ExecuteInitialize_Async()
    {
        settingsMock.SetupGet(m => m.DefaultDocFolder).Returns("\\adrInit\\tests\\docs");
        settingsMock.SetupGet(m => m.DefaultTemplates).Returns("\\adrInit\\tests\\templates");
        settingsMock.Setup(m => m.GetContentFile(It.IsAny<string>())).Returns(contentFileMock.Object);
        contentFileMock.SetupGet(m => m.Exists).Returns(true);
        IAdrInit sut = new AdrInit(settingsMock.Object, logger, repositoryMock.Object, procesMock.Object);
        var result = await sut.InitializeAsync("doc", "template");
        Assert.Equal(0, result);
    }
}
