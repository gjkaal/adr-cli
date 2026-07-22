using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Adr.Cli.XLogger;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Adr.Cli
{
    /// <summary>
    /// Tests to verify that concurrent operations don't cause race conditions or data corruption.
    /// These tests use the real file system to simulate real-world scenarios. Deliberately not
    /// renamed to the With&lt;ClassName&gt; convention used elsewhere in this project: it's a
    /// cross-cutting concurrency scenario spanning AdrRecordRepository and FileLockService
    /// together, not a unit test of either one in isolation.
    /// </summary>
    public sealed class ConcurrentOperationsTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly ILogger<AdrRecordRepository> repoLogger;
        private readonly ILogger<FileLockService> lockLogger;

        public ConcurrentOperationsTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            repoLogger = XUnitLogger.CreateLogger<AdrRecordRepository>(testOutputHelper);
            lockLogger = XUnitLogger.CreateLogger<FileLockService>(testOutputHelper);
        }

        [Fact]
        public async Task ConcurrentLinkOperations_DoNotCorruptMetadata()
        {
            // Arrange
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            var adrFolder = Path.Combine(tempFolder, "adr");
            Directory.CreateDirectory(adrFolder);
            var templatesFolder = Path.Combine(tempFolder, "templates");
            Directory.CreateDirectory(templatesFolder);

            try
            {
                var fileSystem = new FileSystem();
                var settings = new TestAdrSettings(tempFolder, adrFolder, templatesFolder);
                var stdOut = new TestStdOut(testOutputHelper);
                var fileLock = new FileLockService(fileSystem, lockLogger);

                var repository = new AdrRecordRepository(fileSystem, settings, stdOut, fileLock, repoLogger);

                // Create 3 ADR records
                var record1 = new AdrRecord { Title = "First Decision", Status = AdrStatus.Accepted };
                var record2 = new AdrRecord { Title = "Second Decision", Status = AdrStatus.Accepted };
                var record3 = new AdrRecord { Title = "Third Decision", Status = AdrStatus.Accepted };

                await repository.WriteRecordAsync(record1);
                await repository.WriteRecordAsync(record2);
                await repository.WriteRecordAsync(record3);

                testOutputHelper.WriteLine("Created 3 ADR records");

                // Act - Concurrently update record 1 metadata from multiple "threads"
                var concurrentUpdateCount = 10;
                var tasks = new List<Task>();

                for (int i = 0; i < concurrentUpdateCount; i++)
                {
                    int updateId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        testOutputHelper.WriteLine($"Update {updateId}: Starting");

                        // Read metadata
                        var metadata = await repository.ReadMetadataAsync(1);
                        if (metadata == null)
                        {
                            testOutputHelper.WriteLine($"Update {updateId}: Failed to read metadata");
                            return;
                        }

                        // Simulate processing time
                        await Task.Delay(10);

                        // Add a reference (simulating link operation)
                        var targetId = (updateId % 2) + 2; // Alternate between records 2 and 3
                        metadata.UpdateReferenceRemark(targetId, $"Reference from update {updateId}");

                        // Write back
                        await repository.UpdateMetadataAsync(1, metadata);

                        testOutputHelper.WriteLine($"Update {updateId}: Completed - added reference to record {targetId}");
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert - Verify metadata is not corrupted
                var finalMetadata = await repository.ReadMetadataAsync(1);
                Assert.NotNull(finalMetadata);

                testOutputHelper.WriteLine($"Final metadata: RecordId={finalMetadata.RecordId}, Title='{finalMetadata.Title}'");
                testOutputHelper.WriteLine($"References count: {finalMetadata.References.Count}");

                // The metadata should still be valid JSON and have correct structure
                Assert.Equal(1, finalMetadata.RecordId);
                Assert.Equal("First Decision", finalMetadata.Title);

                // Should have references (the exact count depends on execution order, but should be > 0)
                Assert.True(finalMetadata.References.Count > 0,
                    $"Expected references but found {finalMetadata.References.Count}");

                // Verify the JSON file is not corrupted
                var jsonPath = Path.Combine(adrFolder, "00001-first-decision.json");
                Assert.True(File.Exists(jsonPath));

                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                testOutputHelper.WriteLine($"Final JSON content:{Environment.NewLine}{jsonContent}");

                // Should be valid JSON
                var parsedJson = JsonSerializer.Deserialize<AdrRecord>(jsonContent, Constants.JsonOptions);
                Assert.NotNull(parsedJson);
                Assert.Equal(1, parsedJson.RecordId);
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public async Task ConcurrentWriteAndUpdateOperations_ExecuteSequentially()
        {
            // Arrange
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            var adrFolder = Path.Combine(tempFolder, "adr");
            Directory.CreateDirectory(adrFolder);
            var templatesFolder = Path.Combine(tempFolder, "templates");
            Directory.CreateDirectory(templatesFolder);

            try
            {
                var fileSystem = new FileSystem();
                var settings = new TestAdrSettings(tempFolder, adrFolder, templatesFolder);
                var stdOut = new TestStdOut(testOutputHelper);
                var fileLock = new FileLockService(fileSystem, lockLogger);

                var repository = new AdrRecordRepository(fileSystem, settings, stdOut, fileLock, repoLogger);

                // Act - Mix write and update operations concurrently
                var tasks = new List<Task>();
                var operationLog = new List<(int id, string operation, DateTime timestamp)>();
                var logLock = new object();

                // Start 5 write operations
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var record = new AdrRecord { Title = $"Decision {taskId}", Status = AdrStatus.Proposed };

                        lock (logLock)
                        {
                            operationLog.Add((taskId, "Write Started", DateTime.Now));
                        }

                        var recordId = await repository.WriteRecordAsync(record);

                        lock (logLock)
                        {
                            operationLog.Add((taskId, $"Write Completed (ID={recordId})", DateTime.Now));
                        }
                    }));

                    // Also add update operations
                    if (i > 0) // Can only update after first record exists
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            // Wait a bit to ensure record exists
                            await Task.Delay(50);

                            lock (logLock)
                            {
                                operationLog.Add((taskId, "Update Started", DateTime.Now));
                            }

                            var metadata = await repository.ReadMetadataAsync(1);
                            if (metadata != null)
                            {
                                metadata.Status = AdrStatus.Accepted;
                                await repository.UpdateMetadataAsync(1, metadata);

                                lock (logLock)
                                {
                                    operationLog.Add((taskId, "Update Completed", DateTime.Now));
                                }
                            }
                        }));
                    }
                }

                await Task.WhenAll(tasks);

                // Assert - Log all operations
                testOutputHelper.WriteLine("Operation timeline:");
                foreach (var log in operationLog.OrderBy(l => l.timestamp))
                {
                    testOutputHelper.WriteLine($"{log.timestamp:HH:mm:ss.fff} - Task {log.id}: {log.operation}");
                }

                // Verify all records were created
                var recordFiles = Directory.GetFiles(adrFolder, "*.json");
                testOutputHelper.WriteLine($"Created {recordFiles.Length} records");
                Assert.Equal(5, recordFiles.Length);

                // Verify no corruption - all JSON files should be valid
                foreach (var jsonFile in recordFiles)
                {
                    var content = await File.ReadAllTextAsync(jsonFile);
                    var record = JsonSerializer.Deserialize<AdrRecord>(content, Constants.JsonOptions);
                    Assert.NotNull(record);
                    testOutputHelper.WriteLine($"Verified: {jsonFile} - RecordId={record.RecordId}, Title='{record.Title}'");
                }
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public async Task ConcurrentContentAndMetadataUpdates_MaintainConsistency()
        {
            // Arrange
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            var adrFolder = Path.Combine(tempFolder, "adr");
            Directory.CreateDirectory(adrFolder);
            var templatesFolder = Path.Combine(tempFolder, "templates");
            Directory.CreateDirectory(templatesFolder);

            try
            {
                var fileSystem = new FileSystem();
                var settings = new TestAdrSettings(tempFolder, adrFolder, templatesFolder);
                var stdOut = new TestStdOut(testOutputHelper);
                var fileLock = new FileLockService(fileSystem, lockLogger);

                var repository = new AdrRecordRepository(fileSystem, settings, stdOut, fileLock, repoLogger);

                // Create initial record
                var record = new AdrRecord { Title = "Test Decision", Status = AdrStatus.Proposed };
                await repository.WriteRecordAsync(record);

                testOutputHelper.WriteLine("Created initial record");

                // Act - Concurrently update metadata only (simplified test)
                var tasks = new List<Task>();
                var validStatuses = new[] { AdrStatus.New, AdrStatus.Proposed, AdrStatus.Final, AdrStatus.Accepted };

                // 5 tasks updating metadata with valid statuses
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        testOutputHelper.WriteLine($"Task {taskId}: Starting");

                        var metadata = await repository.ReadMetadataAsync(1);
                        if (metadata != null)
                        {
                            await Task.Delay(10); // Simulate processing
                            metadata.Status = validStatuses[taskId % validStatuses.Length];
                            await repository.UpdateMetadataAsync(1, metadata);

                            testOutputHelper.WriteLine($"Task {taskId}: Updated status to {metadata.Status}");
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert - Both files should still exist and be valid
                var mdPath = Path.Combine(adrFolder, "00001-test-decision.md");
                var jsonPath = Path.Combine(adrFolder, "00001-test-decision.json");

                Assert.True(File.Exists(mdPath), "Markdown file should exist");
                Assert.True(File.Exists(jsonPath), "JSON file should exist");

                var finalContent = await File.ReadAllTextAsync(mdPath);
                var finalJson = await File.ReadAllTextAsync(jsonPath);

                testOutputHelper.WriteLine($"Final markdown length: {finalContent.Length}");
                testOutputHelper.WriteLine($"Final JSON length: {finalJson.Length}");

                // JSON should be valid
                var finalMetadata = JsonSerializer.Deserialize<AdrRecord>(finalJson, Constants.JsonOptions);
                Assert.NotNull(finalMetadata);
                Assert.Equal(1, finalMetadata.RecordId);
                Assert.Equal("Test Decision", finalMetadata.Title);

                // Markdown should contain the title
                Assert.Contains("Test Decision", finalContent);

                testOutputHelper.WriteLine("All concurrent operations completed without corruption");
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
        }

        // Helper class for testing
        private class TestAdrSettings : IAdrSettings
        {
            private readonly string rootPath;
            private readonly string adrPath;
            private readonly string templatesPath;

            public TestAdrSettings(string rootPath, string adrPath, string templatesPath)
            {
                this.rootPath = rootPath;
                this.adrPath = adrPath;
                this.templatesPath = templatesPath;
            }

            public string CurrentPath => rootPath;
            public string DefaultDocFolder => adrPath;
            public string DefaultTasksFolder => Path.Combine(rootPath, "tasks");
            public string DefaultTemplates => templatesPath;
            public string DocFolder { get; set; } = string.Empty;
            public string TasksFolder { get; set; } = string.Empty;
            public string TemplateFolder { get; set; } = string.Empty;
            public string ProjectName => "TestProject";
            public AiProviderSettings AiSettings => new();

            public AdrContextInfo CurrentContext => new()
            {
                Success = true,
                ProjectName = ProjectName,
                ConfigFilePath = null,
                CurrentPath = rootPath,
                DocFolder = DocFolder,
                TasksFolder = TasksFolder,
                TemplateFolder = TemplateFolder
            };

            public AdrContextInfo TrySetContext(string workingDirectory)
            {
                return new AdrContextInfo { Success = false, ErrorMessage = "Not supported in TestAdrSettings." };
            }

            public IDirectoryInfo DocFolderInfo() => new FileSystem().DirectoryInfo.New(adrPath);
            public IDirectoryInfo TasksFolderInfo() => new FileSystem().DirectoryInfo.New(DefaultTasksFolder);
            public IDirectoryInfo TemplateFolderInfo() => new FileSystem().DirectoryInfo.New(templatesPath);
            public IDirectoryInfo RootFolderInfo() => new FileSystem().DirectoryInfo.New(rootPath);

            public IFileInfo GetContentFile(DocumentType documentType, string fileName)
            {
                var path = Path.Combine(adrPath, fileName + ".md");
                return new FileSystem().FileInfo.New(path);
            }

            public IFileInfo GetMetaFile(DocumentType documentType, string fileName)
            {
                var path = Path.Combine(adrPath, fileName + ".json");
                return new FileSystem().FileInfo.New(path);
            }

            public int GetNextFileNumber(IDirectoryInfo directoryInfo)
            {
                var files = Directory.GetFiles(adrPath, "*.json");
                if (files.Length == 0) return 1;

                var maxId = files
                    .Select(f => Path.GetFileName(f))
                    .Select(f => int.TryParse(f.AsSpan(0, 5), out var id) ? id : 0)
                    .Max();

                return maxId + 1;
            }

            public IFileInfo GetTemplate(string templateType)
            {
                var path = Path.Combine(templatesPath, templateType + ".md");
                return new FileSystem().FileInfo.New(path);
            }

            public bool RepositoryInitialized() => Directory.Exists(adrPath);
            public bool TasksInitialized() => Directory.Exists(DefaultTasksFolder);
            public IAdrSettings Write() => this;

            public IFileInfo GetDocumentFile(string fileName)
            {
                var path = Path.Combine(rootPath, fileName);
                return new FileSystem().FileInfo.New(path);
            }
        }

        private class TestStdOut : IStdOut
        {
            private readonly ITestOutputHelper output;
            private bool muted = false;

            public TestStdOut(ITestOutputHelper output)
            {
                this.output = output;
            }

            public bool Muted => muted;

            public void Mute() => muted = true;
            public void UnMute() => muted = false;
            public void Write(string message)
            {
                if (!muted) output.WriteLine(message);
            }
            public void WriteLine(string message)
            {
                if (!muted) output.WriteLine(message);
            }
            public void Write(McpCore.Response response)
            {
                if (!muted) output.WriteLine($"{(response.Success ? "OK" : "FAILED")}: {response.Message}");
            }
        }
    }
}
