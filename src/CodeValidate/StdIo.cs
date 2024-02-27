using System.Text;

namespace CodeValidate;

public class StdIo : IStdIo
{
    public StdIo(bool logToFile, bool silent, bool verbose)
    {
        currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
        LogToFile = logToFile;
        Silent = silent;
        Verbose = verbose;
    }

    public void SaveLogfile()
    {
        if (!LogToFile) return;
        if(currentDirectory.Exists)
        {
            var logFile = Path.Combine(currentDirectory.FullName, $"CodeValidate_{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(logFile, log.ToString());
            if (Verbose) Console.WriteLine($"Created logfile: {logFile}");
        }
        else
        {
            if (!Silent) Console.WriteLine("Unable to save logfile: current directory does not exist");
        }
    }

    public bool LogToFile { get; }
    public bool Silent { get; }
    public bool Verbose { get; }

    private readonly StringBuilder log = new();
    private readonly DirectoryInfo currentDirectory;

    public void WriteInfo(string message) {
        if (!Verbose) return;
        if (!Silent) Console.WriteLine(message);
        if (LogToFile) log.AppendLine(message);
    }

    public void WriteError(string message)
    {
        if (!Silent) Console.WriteLine(message);
        if (LogToFile) log.AppendLine(message);
    }

}