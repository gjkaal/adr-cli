using Adr.Cli.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Adr.Cli.Extensions;

/// <summary>
/// Wrapper for System.Diagnostics.Process to enable testing.
/// </summary>
public class ProcessHelper : IProcessHelper
{
    private readonly ILogger<ProcessHelper> logger;
    public ProcessHelper(ILogger<ProcessHelper> logger)
    {
        this.logger = logger;
    }

    public void Start(string fullName)
    {
        try
        {
            Process.Start(fullName);
        }
        catch(FileNotFoundException notFoundEx)
        {
            logger.LogError("File not found", notFoundEx);
            throw new AdrException($"File does not exist: {fullName}");
        }
        catch(Win32Exception)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
    }
    public void Start(ProcessStartInfo processStartInfo)
    {
        Process.Start(processStartInfo);
    }

    public void Start(string fileName, string arguments) 
    {
        Process.Start(fileName, arguments);
    }
}