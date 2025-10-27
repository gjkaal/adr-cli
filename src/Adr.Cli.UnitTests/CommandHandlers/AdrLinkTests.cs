using Adr.Cli;
using Adr.Cli.CommandHandlers;
using Adr.Cli.Services;
using Adr.Cli.XLogger;

using Microsoft.Extensions.Logging;

using Moq;

using System;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Tests.CommandHandlers
{
    public class AdrLinkTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly ILogger<AdrLink> logger;
        private readonly Mock<IStdOut> stdOutMock = new();
        private readonly Mock<IAdrSettings> adrSettingsMock = new();
        private readonly Mock<IAdrRecordRepository> repositoryMock = new();

        public AdrLinkTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            logger = XUnitLogger.CreateLogger<AdrLink>(testOutputHelper);

            stdOutMock.Setup(m => m.WriteLine(It.IsAny<string>()))
                .Callback<string>(s => testOutputHelper.WriteLine(s));
        }

        [Fact]
        public async Task AdrLink_CanLinkTwoAdrs_WithBothMetadataPresent()
        {
            // Arrange
            var sourceId = 1;
            var targetId = 2;
            var reason = "Extends";

            // Source ADR content (markdown)
            var sourceContent = new[]
            {
                "# 00001. First Decision",
                "",
                "2025-01-15",
                "",
                "## Status",
                "",
                "__Proposed__",
                "",
                "## Context",
                "",
                "We need to make a decision.",
                "",
                "## Decision",
                "",
                "We will do something.",
                "",
                "## Consequences",
                "",
                "Things will happen."
            };

            // Target ADR content
            var targetContent = new[]
            {
                "# 00002. Second Decision",
                "",
                "2025-01-16",
                "",
                "## Status",
                "",
                "__Accepted__"
            };

            // Source metadata
            var sourceMeta = new AdrRecord
            {
                RecordId = sourceId,
                FileName = "00001-first-decision",
                Title = "First Decision",
                Status = AdrStatus.Proposed
            };

            // Target metadata
            var targetMeta = new AdrRecord
            {
                RecordId = targetId,
                FileName = "00002-second-decision",
                Title = "Second Decision",
                Status = AdrStatus.Accepted
            };

            // Setup repository mocks
            repositoryMock.Setup(m => m.ReadContentAsync(sourceId))
                .ReturnsAsync(sourceContent);
            repositoryMock.Setup(m => m.ReadContentAsync(targetId))
                .ReturnsAsync(targetContent);
            repositoryMock.Setup(m => m.ReadMetadataAsync(sourceId))
                .ReturnsAsync(sourceMeta);
            repositoryMock.Setup(m => m.ReadMetadataAsync(targetId))
                .ReturnsAsync(targetMeta);

            AdrRecord? capturedMetadata = null;
            string[]? capturedContent = null;

            repositoryMock.Setup(m => m.UpdateMetadataAsync(sourceId, It.IsAny<AdrRecord>()))
                .Callback<int, AdrRecord>((id, record) => capturedMetadata = record)
                .ReturnsAsync(100);

            repositoryMock.Setup(m => m.UpdateContentAsync(It.IsAny<AdrRecord>(), It.IsAny<string[]>()))
                .Callback<AdrRecord, string[]>((record, lines) => capturedContent = lines)
                .ReturnsAsync(500);

            var sut = new AdrLink(logger, repositoryMock.Object, stdOutMock.Object);

            // Act
            var result = await sut.LinkAdrAsync(sourceId, targetId, reason);

            // Assert
            Assert.True(result.Success, "Link operation should succeed");
            Assert.NotNull(capturedMetadata);
            Assert.NotNull(capturedContent);

            // Verify metadata was updated with the reference
            Assert.Contains(targetId, capturedMetadata!.References.Keys);
            Assert.Equal(reason, capturedMetadata.References[targetId]);

            // Verify FileName is still set correctly
            Assert.Equal("00001-first-decision", capturedMetadata.FileName);

            // Verify content is markdown (not JSON)
            var contentString = string.Join("\n", capturedContent!);
            Assert.Contains("# 00001. First Decision", contentString);
            Assert.Contains($"Extends [00002.Second Decision]", contentString);

            // Ensure it's NOT JSON
            Assert.DoesNotContain("{\"RecordId\":", contentString);
            Assert.DoesNotContain("\"FileName\":", contentString);
        }

        [Fact]
        public async Task AdrLink_CanLinkTwoAdrs_WhenSourceMetadataIsMissing()
        {
            // This test reproduces the bug scenario where metadata file is missing
            // and must be reconstructed from markdown

            var sourceId = 1;
            var targetId = 2;
            var reason = "Replaces";

            // Source ADR content (markdown)
            var sourceContent = new[]
            {
                "# 00001. First Decision",
                "",
                "2025-01-15",
                "",
                "## Status",
                "",
                "__Proposed__",
                "",
                "## Context",
                "",
                "We need to make a decision.",
                "",
                "## Decision",
                "",
                "We will do something.",
                "",
                "## Consequences",
                "",
                "Things will happen."
            };

            // Target ADR content and metadata
            var targetContent = new[]
            {
                "# 00002. Second Decision",
                "",
                "2025-01-16"
            };

            var targetMeta = new AdrRecord
            {
                RecordId = targetId,
                FileName = "00002-second-decision",
                Title = "Second Decision",
                Status = AdrStatus.Accepted
            };

            // Setup repository mocks - source metadata is NULL (missing file)
            repositoryMock.Setup(m => m.ReadContentAsync(sourceId))
                .ReturnsAsync(sourceContent);
            repositoryMock.Setup(m => m.ReadContentAsync(targetId))
                .ReturnsAsync(targetContent);
            repositoryMock.Setup(m => m.ReadMetadataAsync(sourceId))
                .ReturnsAsync((AdrRecord?)null); // Metadata file missing!
            repositoryMock.Setup(m => m.ReadMetadataAsync(targetId))
                .ReturnsAsync(targetMeta);

            AdrRecord? capturedMetadata = null;
            string[]? capturedContent = null;
            AdrRecord? capturedContentRecord = null;

            repositoryMock.Setup(m => m.UpdateMetadataAsync(sourceId, It.IsAny<AdrRecord>()))
                .Callback<int, AdrRecord>((id, record) => capturedMetadata = record)
                .ReturnsAsync(100);

            repositoryMock.Setup(m => m.UpdateContentAsync(It.IsAny<AdrRecord>(), It.IsAny<string[]>()))
                .Callback<AdrRecord, string[]>((record, lines) =>
                {
                    capturedContentRecord = record;
                    capturedContent = lines;
                })
                .ReturnsAsync(500);

            var sut = new AdrLink(logger, repositoryMock.Object, stdOutMock.Object);

            // Act
            var result = await sut.LinkAdrAsync(sourceId, targetId, reason);

            // Assert
            Assert.True(result.Success, "Link operation should succeed even when metadata is missing");
            Assert.NotNull(capturedMetadata);
            Assert.NotNull(capturedContent);
            Assert.NotNull(capturedContentRecord);

            // CRITICAL: Verify FileName is properly set (this is where the bug occurs)
            Assert.NotNull(capturedMetadata!.FileName);
            Assert.NotEmpty(capturedMetadata.FileName);
            Assert.Equal("00001-first-decision", capturedMetadata.FileName);

            // Verify the content record also has correct FileName
            Assert.NotNull(capturedContentRecord!.FileName);
            Assert.NotEmpty(capturedContentRecord.FileName);
            Assert.Equal("00001-first-decision", capturedContentRecord.FileName);

            // Verify metadata contains the link
            Assert.Contains(targetId, capturedMetadata.References.Keys);
            Assert.Equal(reason, capturedMetadata.References[targetId]);

            // Verify content is markdown (not JSON)
            var contentString = string.Join("\n", capturedContent!);
            Assert.Contains("# 00001. First Decision", contentString);
            Assert.Contains($"{reason} [00002.Second Decision]", contentString);

            // Ensure it's NOT JSON (the bug would cause JSON to be written to .md file)
            Assert.DoesNotContain("{\"RecordId\":", contentString);
            Assert.DoesNotContain("\"FileName\":", contentString);
            Assert.DoesNotContain("\"References\":", contentString);
        }

        [Fact]
        public async Task AdrLink_RemoveLink_WhenMetadataIsMissing()
        {
            // Test removing link when metadata must be reconstructed
            var sourceId = 1;
            var targetId = 2;

            var sourceContent = new[]
            {
                "# 00001. First Decision",
                "",
                "## Status",
                "",
                "__Proposed__",
                "",
                "Extends [00002.Second Decision](./00002-second-decision.md)",
                "",
                "## Context",
                "",
                "Context here."
            };

            // Source metadata is NULL (missing file)
            repositoryMock.Setup(m => m.ReadContentAsync(sourceId))
                .ReturnsAsync(sourceContent);
            repositoryMock.Setup(m => m.ReadMetadataAsync(sourceId))
                .ReturnsAsync((AdrRecord?)null);

            AdrRecord? capturedMetadata = null;
            AdrRecord? capturedContentRecord = null;

            repositoryMock.Setup(m => m.UpdateMetadataAsync(sourceId, It.IsAny<AdrRecord>()))
                .Callback<int, AdrRecord>((id, record) => capturedMetadata = record)
                .ReturnsAsync(100);

            repositoryMock.Setup(m => m.UpdateContentAsync(It.IsAny<AdrRecord>(), It.IsAny<string[]>()))
                .Callback<AdrRecord, string[]>((record, lines) => capturedContentRecord = record)
                .ReturnsAsync(500);

            var sut = new AdrLink(logger, repositoryMock.Object, stdOutMock.Object);

            // Act
            var result = await sut.RemoveLinkAsync(sourceId, targetId);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(capturedMetadata);
            Assert.NotNull(capturedContentRecord);

            // Verify FileName is properly set
            Assert.NotNull(capturedMetadata!.FileName);
            Assert.NotEmpty(capturedMetadata.FileName);
            Assert.Equal("00001-first-decision", capturedMetadata.FileName);

            Assert.NotNull(capturedContentRecord!.FileName);
            Assert.NotEmpty(capturedContentRecord.FileName);
            Assert.Equal("00001-first-decision", capturedContentRecord.FileName);
        }
    }
}
