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

    /// <summary>
    /// Whether an AI provider is configured (see AI-Setup.md) - i.e. whether --ai / "ai": true
    /// will actually draft content rather than being a no-op.
    /// </summary>
    public bool AiConfigured { get; set; }

    /// <summary>
    /// The configured AI provider name (e.g. "AzureFoundry"), or empty when none is configured.
    /// </summary>
    public string AiProvider { get; set; } = string.Empty;

    /// <summary>
    /// Whether a task sync connector is configured - i.e. whether <c>task-export</c>/
    /// <c>task-import</c> will actually reach an external provider rather than being a no-op.
    /// </summary>
    public bool SyncConfigured { get; set; }

    /// <summary>
    /// The configured sync provider name (e.g. "GitHubProjects", "AzureDevOps"), or empty when none
    /// is configured.
    /// </summary>
    public string SyncProvider { get; set; } = string.Empty;
}
