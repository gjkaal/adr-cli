using Adr.Cli.Sync;

using System.IO.Abstractions;

namespace Adr.Cli;

public enum DocumentType
{
    None,
    Adr,
    Task
}

public interface IAdrSettings
{
    /// <summary>
    /// The current folder location.
    /// </summary>
    string CurrentPath { get; }

    /// <summary>
    /// The default location for Adr records.
    /// </summary>
    string DefaultDocFolder { get; }

    /// <summary>
    /// The default location for planning records.
    /// </summary>
    string DefaultTasksFolder { get; }

    /// <summary>
    /// The default location for Adr templates.
    /// </summary>
    string DefaultTemplates { get; }

    /// <summary>
    /// Relative or full file location for the documentation folder.
    /// </summary>
    string DocFolder { get; set; }

    /// <summary>
    /// Relative or full file location for the documentation folder.
    /// </summary>
    string TasksFolder { get; set; }

    /// <summary>
    /// Relative or full file location for templates.
    /// </summary>
    string TemplateFolder { get; set; }

    /// <summary>
    /// The name for the ADR project.
    /// </summary>
    string ProjectName { get; }

    /// <summary>
    /// Describes which adr.config.json is currently active for this process.
    /// </summary>
    AdrContextInfo CurrentContext { get; }

    /// <summary>
    /// Configuration for the optional AI provider used to draft ADR proposals. An empty
    /// <see cref="AiProviderSettings.Provider" /> means AI generation is not configured.
    /// </summary>
    AiProviderSettings AiSettings { get; }

    /// <summary>
    /// Configuration for the optional task sync connector (Azure DevOps, GitHub Projects, ...). An
    /// empty <see cref="TaskSyncProviderSettings.Provider" /> means no connector is configured, and
    /// the registered <c>ITaskSyncProvider</c> will be a no-op. Only one connector is active at a
    /// time; see <see cref="TaskSyncProviderSettings" /> for why its settings are opaque here.
    /// </summary>
    TaskSyncProviderSettings SyncSettings { get; }

    /// <summary>
    /// Re-resolve settings from the adr.config.json found by searching upward from
    /// <paramref name="workingDirectory" />, without creating anything. Intended for a long-lived
    /// MCP server process to be pointed at a specific, already-initialized repository instead of
    /// staying fixed to whatever directory the process happened to start in.
    /// </summary>
    /// <param name="workingDirectory">
    /// Any directory inside the target ADR repository.
    /// </param>
    /// <returns>
    /// On success, the resolved context. On failure (no config.json found in that directory or any
    /// parent), a failed result with an explanatory message; existing settings are left unchanged.
    /// </returns>
    AdrContextInfo TrySetContext(string workingDirectory);

    /// <summary>
    /// Directory information for the ADR documents.
    /// </summary>
    IDirectoryInfo DocFolderInfo();

    /// <summary>
    /// Directory information for the ADR documents.
    /// </summary>
    IDirectoryInfo TasksFolderInfo();

    /// <summary>
    /// Directory information for the ADR templates.
    /// </summary>
    IDirectoryInfo TemplateFolderInfo();

    /// <summary>
    /// Directory information for the folder where the settings file is located.
    /// </summary>
    IDirectoryInfo RootFolderInfo();

    /// <summary>
    /// Find a content file with the provided base name.
    /// </summary>
    /// <param name="fileName">
    /// Any string
    /// </param>
    /// <remarks>
    /// the filename will be sanitized.
    /// </remarks>
    /// <returns>
    /// a file information object that can be used to manage the content file.
    /// </returns>
    IFileInfo GetContentFile(DocumentType documentType, string fileName);

    /// <summary>
    /// Find a metadata file with the provided base name.
    /// </summary>
    /// <param name="fileName">
    /// Any string
    /// </param>
    /// <remarks>
    /// the filename will be sanitized.
    /// </remarks>
    /// <returns>
    /// a file information object that can be used to manage the metadata file.
    /// </returns>
    IFileInfo GetMetaFile(DocumentType documentType, string fileName);

    /// <summary>
    /// Find the next file identification, starting with 0 (zero) for an uninitialized ADR folder.
    /// </summary>
    /// <returns>
    /// A positive integer number.
    /// </returns>
    int GetNextFileNumber(IDirectoryInfo directoryInfo);

    /// <summary>
    /// Find the next file identification for a template.
    /// </summary>
    /// <param name="templateType">
    /// </param>
    /// <returns>
    /// A file information class ( <see cref="IFileInfo" />).
    /// </returns>
    IFileInfo GetTemplate(string templateType);

    /// <summary>
    /// A boolean indicating that the ADR repository is initialized. A new initialization should be blocked.
    /// </summary>
    /// <returns>
    /// True if the repository is already initialized.
    /// </returns>
    bool RepositoryInitialized();

    /// <summary>
    /// A boolean indicating that the tasks repository is initialized. A new initialization should
    /// be blocked.
    /// </summary>
    /// <returns>
    /// True if the repository is already initialized.
    /// </returns>
    bool TasksInitialized();

    /// <summary>
    /// Save the current settings in a conmfiguration file
    /// </summary>
    /// <returns>
    /// The current IAdrSettings settings
    /// </returns>
    IAdrSettings Write();

    /// <summary>
    /// Find a documentation file. Typically, a documentation file resides next to the config file
    /// in the <see cref="CurrentPath" /> folder.
    /// </summary>
    /// <param name="fileName">
    /// A valid file name.
    /// </param>
    /// <returns>
    /// A file information class ( <see cref="IFileInfo" />) for managing additional project documents.
    /// </returns>
    IFileInfo GetDocumentFile(string fileName);
}
