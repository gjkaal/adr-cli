using Adr.Cli.Services;
using Adr.Cli.XLogger;

using Microsoft.Extensions.Logging;

using Moq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace Tests.Services
{
    public class FileLockServiceTests
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly ILogger<FileLockService> logger;

        public FileLockServiceTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            logger = XUnitLogger.CreateLogger<FileLockService>(testOutputHelper);
        }

        [Fact]
        public async Task FileLockService_CanAcquireAndReleaseLock()
        {
            // Arrange
            var fileSystem = new FileSystem();
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempFolder);

            try
            {
                var fileLock = new FileLockService(fileSystem, logger);
                var lockFilePath = System.IO.Path.Combine(tempFolder, "LOCK.TMP");

                // Act
                using (var lockHandle = await fileLock.AcquireLockAsync(tempFolder, "TestOperation"))
                {
                    // Assert - Lock file should exist while locked
                    Assert.True(System.IO.File.Exists(lockFilePath));

                    var lockContent = System.IO.File.ReadAllText(lockFilePath);
                    testOutputHelper.WriteLine($"Lock file content:{Environment.NewLine}{lockContent}");

                    Assert.Contains("Operation: TestOperation", lockContent);
                    Assert.Contains("Timestamp:", lockContent);
                    Assert.Contains("Process ID:", lockContent);
                }

                // Assert - Lock file should be deleted after disposal
                Assert.False(System.IO.File.Exists(lockFilePath));
            }
            finally
            {
                if (System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public async Task FileLockService_ConcurrentOperations_ExecuteSequentially()
        {
            // Arrange
            var fileSystem = new FileSystem();
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempFolder);

            try
            {
                var fileLock = new FileLockService(fileSystem, logger);
                var executionOrder = new List<(int taskId, string action, DateTime timestamp)>();
                var lockObject = new object();

                // Act - Start 5 concurrent operations
                var tasks = new List<Task>();
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        lock (lockObject)
                        {
                            executionOrder.Add((taskId, "Started", DateTime.Now));
                        }

                        using (var lockHandle = await fileLock.AcquireLockAsync(tempFolder, $"Operation_{taskId}"))
                        {
                            lock (lockObject)
                            {
                                executionOrder.Add((taskId, "Acquired", DateTime.Now));
                            }

                            // Simulate work
                            await Task.Delay(50);

                            lock (lockObject)
                            {
                                executionOrder.Add((taskId, "Released", DateTime.Now));
                            }
                        }

                        lock (lockObject)
                        {
                            executionOrder.Add((taskId, "Completed", DateTime.Now));
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert - Log execution order
                testOutputHelper.WriteLine("Execution order:");
                foreach (var entry in executionOrder)
                {
                    testOutputHelper.WriteLine($"Task {entry.taskId}: {entry.action} at {entry.timestamp:HH:mm:ss.fff}");
                }

                // Extract acquired and released events
                var acquiredEvents = executionOrder.Where(e => e.action == "Acquired").ToList();
                var releasedEvents = executionOrder.Where(e => e.action == "Released").ToList();

                // Verify that no two tasks held the lock simultaneously
                for (int i = 0; i < acquiredEvents.Count - 1; i++)
                {
                    var currentAcquired = acquiredEvents[i];
                    var currentReleased = releasedEvents.First(r => r.taskId == currentAcquired.taskId);
                    var nextAcquired = acquiredEvents[i + 1];

                    testOutputHelper.WriteLine($"Verifying: Task {currentAcquired.taskId} released at {currentReleased.timestamp:HH:mm:ss.fff} before Task {nextAcquired.taskId} acquired at {nextAcquired.timestamp:HH:mm:ss.fff}");

                    // The next task should acquire AFTER the current task released
                    Assert.True(nextAcquired.timestamp >= currentReleased.timestamp,
                        $"Task {nextAcquired.taskId} acquired lock before Task {currentAcquired.taskId} released it!");
                }
            }
            finally
            {
                if (System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public async Task FileLockService_ExceptionDuringLock_StillReleasesLock()
        {
            // Arrange
            var fileSystem = new FileSystem();
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempFolder);

            try
            {
                var fileLock = new FileLockService(fileSystem, logger);
                var lockFilePath = System.IO.Path.Combine(tempFolder, "LOCK.TMP");

                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    using (var lockHandle = await fileLock.AcquireLockAsync(tempFolder, "TestOperation"))
                    {
                        Assert.True(System.IO.File.Exists(lockFilePath));
                        throw new InvalidOperationException("Simulated exception");
                    }
                });

                // Assert - Lock file should be deleted even after exception
                Assert.False(System.IO.File.Exists(lockFilePath));

                // Verify we can acquire the lock again
                using (var lockHandle = await fileLock.AcquireLockAsync(tempFolder, "SecondOperation"))
                {
                    Assert.True(System.IO.File.Exists(lockFilePath));
                }
            }
            finally
            {
                if (System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public async Task FileLockService_ConcurrentWrites_PreventDataCorruption()
        {
            // Arrange
            var fileSystem = new FileSystem();
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempFolder);

            try
            {
                var fileLock = new FileLockService(fileSystem, logger);
                var testFilePath = System.IO.Path.Combine(tempFolder, "test.txt");
                var writeCount = 10;
                var successCount = 0;

                // Act - Simulate concurrent writes to the same file
                var tasks = new List<Task>();
                for (int i = 0; i < writeCount; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        using (var lockHandle = await fileLock.AcquireLockAsync(tempFolder, $"Write_{taskId}"))
                        {
                            // Read current count
                            int currentCount = 0;
                            if (System.IO.File.Exists(testFilePath))
                            {
                                var content = await System.IO.File.ReadAllTextAsync(testFilePath);
                                if (!string.IsNullOrEmpty(content))
                                {
                                    currentCount = int.Parse(content);
                                }
                            }

                            // Simulate processing time
                            await Task.Delay(10);

                            // Increment and write
                            currentCount++;
                            await System.IO.File.WriteAllTextAsync(testFilePath, currentCount.ToString());

                            testOutputHelper.WriteLine($"Task {taskId}: wrote {currentCount}");

                            System.Threading.Interlocked.Increment(ref successCount);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert
                var finalContent = await System.IO.File.ReadAllTextAsync(testFilePath);
                var finalCount = int.Parse(finalContent);

                testOutputHelper.WriteLine($"Final count: {finalCount}");
                testOutputHelper.WriteLine($"Expected count: {writeCount}");
                testOutputHelper.WriteLine($"Successful writes: {successCount}");

                // If locking works correctly, each increment should be atomic
                Assert.Equal(writeCount, finalCount);
                Assert.Equal(writeCount, successCount);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.Delete(tempFolder, true);
                }
            }
        }

        [Fact]
        public async Task FileLockService_RemovesStaleLock()
        {
            // Arrange
            var fileSystem = new FileSystem();
            var tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempFolder);

            try
            {
                var fileLock = new FileLockService(fileSystem, logger);
                var lockFilePath = System.IO.Path.Combine(tempFolder, "LOCK.TMP");

                // Create a stale lock file (older than 5 minutes)
                var staleLockContent = $"Operation: StaleLock{Environment.NewLine}" +
                                       $"Timestamp: {DateTime.Now.AddMinutes(-10):yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}" +
                                       $"Process ID: 99999{Environment.NewLine}";
                await System.IO.File.WriteAllTextAsync(lockFilePath, staleLockContent);

                // Make the file appear old
                System.IO.File.SetLastWriteTime(lockFilePath, DateTime.Now.AddMinutes(-10));

                testOutputHelper.WriteLine($"Created stale lock at: {System.IO.File.GetLastWriteTime(lockFilePath):yyyy-MM-dd HH:mm:ss.fff}");

                // Act - Try to acquire lock (should remove stale lock and succeed)
                var sw = Stopwatch.StartNew();
                using (var lockHandle = await fileLock.AcquireLockAsync(tempFolder, "NewOperation"))
                {
                    sw.Stop();
                    testOutputHelper.WriteLine($"Acquired lock in {sw.ElapsedMilliseconds}ms");

                    // Assert
                    Assert.True(System.IO.File.Exists(lockFilePath));
                    var newLockContent = await System.IO.File.ReadAllTextAsync(lockFilePath);
                    Assert.Contains("Operation: NewOperation", newLockContent);
                    Assert.DoesNotContain("StaleLock", newLockContent);
                }

                // Lock should complete relatively quickly (not wait 30 seconds)
                Assert.True(sw.ElapsedMilliseconds < 5000, $"Lock took too long: {sw.ElapsedMilliseconds}ms");
            }
            finally
            {
                if (System.IO.Directory.Exists(tempFolder))
                {
                    System.IO.Directory.Delete(tempFolder, true);
                }
            }
        }
    }
}
