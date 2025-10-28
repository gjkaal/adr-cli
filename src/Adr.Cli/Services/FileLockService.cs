using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Adr.Cli.Services;

/// <summary>
/// Implements file-based locking mechanism using LOCK.TMP file to prevent concurrent file operations.
/// This works across processes, unlike in-memory locking mechanisms.
/// </summary>
public class FileLockService : IFileLock
{
    private readonly IFileSystem fileSystem;
    private readonly ILogger<FileLockService> logger;
    private const string LockFileName = "LOCK.TMP";
    private const int MaxWaitTimeMs = 30000; // 30 seconds max wait
    private const int RetryDelayMs = 100; // Check every 100ms

    public FileLockService(IFileSystem fileSystem, ILogger<FileLockService> logger)
    {
        this.fileSystem = fileSystem;
        this.logger = logger;
    }

    public async Task<IDisposable> AcquireLockAsync(string folderPath, string operationType)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            throw new ArgumentNullException(nameof(folderPath));
        }

        if (string.IsNullOrEmpty(operationType))
        {
            throw new ArgumentNullException(nameof(operationType));
        }

        var lockFilePath = fileSystem.Path.Combine(folderPath, LockFileName);
        var startTime = DateTime.Now;
        var waited = false;

        // Wait for any existing lock to be released
        while (fileSystem.File.Exists(lockFilePath))
        {
            if (!waited)
            {
                logger.LogInformation("Waiting for lock file to be released: {LockFilePath}", lockFilePath);
                waited = true;
            }

            // Check if lock file is stale BEFORE checking timeout
            var lockFileInfo = fileSystem.FileInfo.New(lockFilePath);
            var lockAge = DateTime.Now - lockFileInfo.LastWriteTime;

            if (lockAge.TotalMinutes > 5)
            {
                logger.LogWarning("Lock file is stale (age: {LockAge}), removing it: {LockFilePath}",
                    lockAge, lockFilePath);
                try
                {
                    fileSystem.File.Delete(lockFilePath);
                    break; // Exit the loop to proceed with lock creation
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete stale lock file: {LockFilePath}", lockFilePath);
                    throw new TimeoutException(
                        $"Failed to remove stale lock file on folder '{folderPath}'.", ex);
                }
            }

            var elapsedTime = DateTime.Now - startTime;
            if (elapsedTime.TotalMilliseconds > MaxWaitTimeMs)
            {
                throw new TimeoutException(
                    $"Timeout waiting for lock on folder '{folderPath}'. Another operation may be in progress.");
            }

            await Task.Delay(RetryDelayMs);
        }

        if (waited)
        {
            logger.LogInformation("Lock file released, proceeding with operation");
        }

        // Create the lock file atomically
        var lockContent = $"Operation: {operationType}{Environment.NewLine}" +
                         $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}" +
                         $"Process ID: {Environment.ProcessId}{Environment.NewLine}";

        // Ensure directory exists
        var directoryPath = fileSystem.Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrEmpty(directoryPath) && !fileSystem.Directory.Exists(directoryPath))
        {
            fileSystem.Directory.CreateDirectory(directoryPath);
        }

        // Try to create the lock file atomically with retries
        var lockCreated = false;
        var retryStartTime = DateTime.Now;

        while (!lockCreated)
        {
            try
            {
                // Use FileMode.CreateNew for atomic creation - will fail if file exists
                using (var stream = fileSystem.FileStream.New(lockFilePath, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
                using (var writer = new System.IO.StreamWriter(stream))
                {
                    writer.Write(lockContent);
                    writer.Flush();
                }
                lockCreated = true;
                logger.LogDebug("Acquired lock for {OperationType} in {FolderPath}", operationType, folderPath);
            }
            catch (System.IO.IOException ioEx) when (ioEx.Message.Contains("already exists") || fileSystem.File.Exists(lockFilePath))
            {
                // File exists or was just created by another thread - wait and retry
                var elapsed = DateTime.Now - retryStartTime;
                if (elapsed.TotalMilliseconds > MaxWaitTimeMs)
                {
                    if (fileSystem.File.Exists(lockFilePath))
                    {
                        // Check if lock file is stale
                        var lockFileInfo = fileSystem.FileInfo.New(lockFilePath);
                        var lockAge = DateTime.Now - lockFileInfo.LastWriteTime;

                        if (lockAge.TotalMinutes > 5)
                        {
                            logger.LogWarning("Lock file is stale (age: {LockAge}), removing it: {LockFilePath}",
                                lockAge, lockFilePath);
                            try
                            {
                                fileSystem.File.Delete(lockFilePath);
                                // Don't set lockCreated, let next iteration try to create
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to delete stale lock file: {LockFilePath}", lockFilePath);
                                throw new TimeoutException(
                                    $"Timeout waiting for lock on folder '{folderPath}' and failed to remove stale lock.", ex);
                            }
                        }
                        else
                        {
                            throw new TimeoutException(
                                $"Timeout waiting for lock on folder '{folderPath}'. Another operation may be in progress.");
                        }
                    }
                    else
                    {
                        throw new TimeoutException(
                            $"Timeout waiting for lock on folder '{folderPath}'. Lock file disappeared.");
                    }
                }

                // Wait before retrying
                await Task.Delay(RetryDelayMs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create lock file: {LockFilePath}", lockFilePath);
                throw;
            }
        }

        // Return a disposable object that will delete the lock file when disposed
        return new FileLockHandle(fileSystem, logger, lockFilePath, operationType);
    }

    /// <summary>
    /// Disposable handle for a file lock that ensures the lock file is always deleted.
    /// </summary>
    private class FileLockHandle : IDisposable
    {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private readonly string lockFilePath;
        private readonly string operationType;
        private bool disposed = false;

        public FileLockHandle(IFileSystem fileSystem, ILogger logger, string lockFilePath, string operationType)
        {
            this.fileSystem = fileSystem;
            this.logger = logger;
            this.lockFilePath = lockFilePath;
            this.operationType = operationType;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            try
            {
                if (fileSystem.File.Exists(lockFilePath))
                {
                    fileSystem.File.Delete(lockFilePath);
                    logger.LogDebug("Released lock for {OperationType}: {LockFilePath}", operationType, lockFilePath);
                }
                else
                {
                    logger.LogWarning("Lock file already deleted: {LockFilePath}", lockFilePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete lock file during disposal: {LockFilePath}", lockFilePath);
                // Don't throw in Dispose - just log the error
            }
            finally
            {
                disposed = true;
            }
        }
    }
}
