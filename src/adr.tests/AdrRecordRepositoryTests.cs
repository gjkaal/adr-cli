using adr;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System;
using Xunit;
using Xunit.Abstractions;
using Tests.XLogger;
using System.Threading.Tasks;
using System.IO.Abstractions;
using System.IO;
using Newtonsoft.Json;
using adr.Services;

namespace Tests
{
    public class AdrRecordRepositoryTests 
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly ILogger<AdrRecordRepository> logger;
        private readonly Mock<IStdOut> stdOutMock = new();
        private readonly Mock<IAdrSettings> adrSettingsMock = new();
        private readonly Mock<IFileSystem> fileSystemMock = new();
        private readonly Mock<IDirectoryInfo> docFolderMock = new();
        private readonly Mock<IDirectoryInfo> templateFolderMock = new();        

        // see https://www.meziantou.net/how-to-get-asp-net-core-logs-in-the-output-of-xunit-tests.htm
        // for information about xunit ilogger interception

        public AdrRecordRepositoryTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            logger = XUnitLogger.CreateLogger<AdrRecordRepository>(testOutputHelper);

            adrSettingsMock.Setup(m => m.DocFolderInfo()).Returns(docFolderMock.Object);
            adrSettingsMock.Setup(m => m.TemplateFolderInfo()).Returns(templateFolderMock.Object);
            docFolderMock.SetupGet(m => m.FullName).Returns("x:\\temp\\adr\\doc");
            templateFolderMock.SetupGet(m => m.FullName).Returns("x:\\temp\\adr\\template");

            stdOutMock.Setup(m => m.WriteLine(It.IsAny<string>())).Callback<string>(s => testOutputHelper.WriteLine(s));
        }

        [Fact]
        public async Task AdrRecordRepository_CanWriteRecords()
        {
            var fileInfoMock = new Mock<IFileInfo>();
            fileInfoMock.SetupGet(m => m.Exists).Returns(false);
            adrSettingsMock.Setup(m => m.GetTemplate(It.IsAny<string>())).Returns(fileInfoMock.Object);

            using var stream1 = new MemoryStream();
            using var writer1 = new StreamWriter(stream1);
            var fileStream1 = new Mock<IFileInfo>();
            fileStream1.Setup(m => m.CreateText()).Returns(writer1);

            using var stream2 = new MemoryStream();
            using var writer2 = new StreamWriter(stream2);
            var fileStream2 = new Mock<IFileInfo>();
            fileStream2.Setup(m => m.CreateText()).Returns(writer2);

            using var stream3 = new MemoryStream();
            using var writer3 = new StreamWriter(stream3);
            var fileStream3 = new Mock<IFileInfo>();
            fileStream3.Setup(m => m.CreateText()).Returns(writer3);

            adrSettingsMock.Setup(m => m.GetContentFile(It.IsAny<string>())).Returns(fileStream1.Object);
            adrSettingsMock.Setup(m => m.GetMetaFile(It.IsAny<string>())).Returns(fileStream2.Object);
            adrSettingsMock.Setup(m => m.GetTemplate(It.IsAny<string>())).Returns(fileStream3.Object);

            adrSettingsMock.Setup(m => m.GetNextFileNumber()).Returns(167);

            var record = new AdrRecord { 
                RecordId = 123,
                Title = "Test",
            };
            IAdrRecordRepository sut = new AdrRecordRepository(fileSystemMock.Object, adrSettingsMock.Object, stdOutMock.Object, logger);

            await sut.WriteRecordAsync(record);

            var content = Encoding.UTF8.GetString(stream1.ToArray());
            var metadata = Encoding.UTF8.GetString(stream2.ToArray());

            Assert.True(content.Length > 0);
            Assert.True(metadata.Length > 0);

            testOutputHelper.WriteLine("Content:");
            testOutputHelper.WriteLine(content);
            testOutputHelper.WriteLine("");
            testOutputHelper.WriteLine("Metadata:");
            testOutputHelper.WriteLine(metadata);

            Assert.Equal("# 00167. Test", content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0]);
            var metaRecord = JsonConvert.DeserializeObject<AdrRecord>(metadata);
            Assert.Equal(record.RecordId, metaRecord.RecordId);
            Assert.Equal(record.Title, metaRecord.Title);
        }

        [Fact]
        public void AdrRecordRepository_CanInitialize()
        {
            IAdrRecordRepository sut = new AdrRecordRepository(fileSystemMock.Object, adrSettingsMock.Object, stdOutMock.Object, logger);
            Assert.NotNull(sut);
        }

        [Theory]
        [InlineData(TemplateType.Init)]
        [InlineData(TemplateType.Revision)]
        [InlineData(TemplateType.Asr)]
        [InlineData(TemplateType.Ad)]
        public async Task AdrRecordRepository_GetLayoutAsync_ProvidesLayoutFromTemplate(TemplateType template)
        {
            var content = Encoding.UTF8.GetBytes($"THIS IS THE TEMPLATE {template}");
            using var fileStream = new MemoryStream(content);
            using var streamReader = new StreamReader(fileStream);

            var fileInfoMock = new Mock<IFileInfo>();
            fileInfoMock.SetupGet(m => m.Exists).Returns(true);
            fileInfoMock.Setup(m => m.OpenText()).Returns(streamReader);
            adrSettingsMock.Setup(m => m.GetTemplate(It.IsAny<string>())).Returns(fileInfoMock.Object);

            IAdrRecordRepository sut = new AdrRecordRepository(fileSystemMock.Object, adrSettingsMock.Object, stdOutMock.Object, logger);
            var record = new AdrRecord
            {
                TemplateType = template
            };
            var layout = await sut.GetLayoutAsync(record);

            Assert.NotNull(layout);
            Assert.True(layout.Length>0);
            testOutputHelper.WriteLine(layout.ToString());
            Assert.Equal($"THIS IS THE TEMPLATE {template}", layout.ToString());
        }

        [Fact]
        public async Task AdrRecordRepository_GetLayoutAsync_ProvidesLayoutWithoutTemplate()
        {
            using var stream3 = new MemoryStream();
            using var writer3 = new StreamWriter(stream3);
            var fileStream3 = new Mock<IFileInfo>();
            fileStream3.Setup(m => m.CreateText()).Returns(writer3);
            fileStream3.SetupGet(m => m.Exists).Returns((bool)false);
            adrSettingsMock.Setup(m => m.GetTemplate(It.IsAny<string>())).Returns(fileStream3.Object);

            IAdrRecordRepository sut = new AdrRecordRepository(fileSystemMock.Object, adrSettingsMock.Object, stdOutMock.Object, logger);
            var record = new AdrRecord
            {
                TemplateType = TemplateType.Ad
            };
            var layout = await sut.GetLayoutAsync(record);

            Assert.NotNull(layout);
            Assert.True(layout.Length > 0);
            testOutputHelper.WriteLine(layout.ToString());
        }
    }
}