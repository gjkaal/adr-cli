using System;
using System.Threading.Tasks;

namespace Adr.Cli.Services;

/// <summary>
/// Provides file-based locking mechanism to prevent concurrent file operations across processes.
/// </summary>
public interface IFileLock
{
    /// <summary>
    /// Acquires a file-based lock for a specific folder.
    /// </summary>
    /// <param name="folderPath">The folder to lock</param>
    /// <param name="operationType">The type of operation being performed</param>
    /// <returns>A disposable lock object that will release the lock when disposed</returns>
    Task<IDisposable> AcquireLockAsync(string folderPath, string operationType);
}
