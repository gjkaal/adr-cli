namespace Adr.Cli;

/// <summary>
/// Describes which adr.config.json is currently active for the process, or the outcome of
/// an attempt to switch to a different one.
/// </summary>
public class AdrContextInfo
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? ConfigFilePath { get; set; }
    public string CurrentPath { get; set; } = string.Empty;
    public string DocFolder { get; set; } = string.Empty;
    public string TasksFolder { get; set; } = string.Empty;
    public string TemplateFolder { get; set; } = string.Empty;
}
