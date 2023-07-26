using adr;
using CommandHandlers;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Tests.XLogger;
using Xunit;
using Xunit.Abstractions;

namespace Tests.CommandHandlers;
public class AdrInitCommandHandlerTests
{
    private readonly Mock<IAdrSettings> settingsMock = new Mock<IAdrSettings>();
    private readonly Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
    private readonly Mock<IAdrRecordRepository> repositoryMock = new Mock<IAdrRecordRepository>();

    private readonly ITestOutputHelper testOutputHelper;
    private readonly ILogger<AdrInit> logger;

    // see https://www.meziantou.net/how-to-get-asp-net-core-logs-in-the-output-of-xunit-tests.htm
    // for information about xunit ilogger interception

    public AdrInitCommandHandlerTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
        logger = XUnitLogger.CreateLogger<AdrInit>(testOutputHelper);
    }

    [Fact]
    public void AdrInitCommandHandler_CanInitialize()
    {
        IAdrInit sut = new AdrInitCommandHandler(
            new Mock<IAdrSettings>().Object, 
            new Mock<IFileSystem>().Object, 
            logger, 
            new Mock<IAdrRecordRepository>().Object);
        Assert.NotNull(sut);
    }

    [Fact]
    public async Task AdrInitCommandHandler_CanStart_Async()
    {
        IAdrInit sut = new AdrInitCommandHandler(settingsMock.Object, fileSystemMock.Object, logger, repositoryMock.Object);
        var result = await sut.InitializeAsync("doc", "template");
        Assert.Equal(1, result);
    }
}
