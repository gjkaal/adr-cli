using System;
using System.IO.Abstractions;
using System.Text.Json;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// Minimal IAdrSettings stub - GitHubProjectsTaskSyncProvider only ever reads SyncSettings, so
/// every other member is unused and left throwing.
/// </summary>
internal sealed class FixedSyncSettings : IAdrSettings
{
    public FixedSyncSettings(string settingsJson)
    {
        SyncSettings = new TaskSyncProviderSettings
        {
            Provider = GitHubProjectsTaskSyncProvider.ProviderName,
            Settings = JsonDocument.Parse(settingsJson).RootElement.Clone()
        };
    }

    public TaskSyncProviderSettings SyncSettings { get; }
    public AiProviderSettings AiSettings => throw new NotSupportedException();

    public string CurrentPath => throw new NotSupportedException();
    public string DefaultDocFolder => throw new NotSupportedException();
    public string DefaultTasksFolder => throw new NotSupportedException();
    public string DefaultTemplates => throw new NotSupportedException();
    public string DocFolder { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public string TasksFolder { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public string TemplateFolder { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public string ProjectName => throw new NotSupportedException();
    public AdrContextInfo CurrentContext => throw new NotSupportedException();

    public AdrContextInfo TrySetContext(string workingDirectory) => throw new NotSupportedException();
    public IDirectoryInfo DocFolderInfo() => throw new NotSupportedException();
    public IDirectoryInfo TasksFolderInfo() => throw new NotSupportedException();
    public IDirectoryInfo TemplateFolderInfo() => throw new NotSupportedException();
    public IDirectoryInfo RootFolderInfo() => throw new NotSupportedException();
    public IFileInfo GetContentFile(DocumentType documentType, string fileName) => throw new NotSupportedException();
    public IFileInfo GetMetaFile(DocumentType documentType, string fileName) => throw new NotSupportedException();
    public int GetNextFileNumber(IDirectoryInfo directoryInfo) => throw new NotSupportedException();
    public IFileInfo GetTemplate(string templateType) => throw new NotSupportedException();
    public bool RepositoryInitialized() => throw new NotSupportedException();
    public bool TasksInitialized() => throw new NotSupportedException();
    public IAdrSettings Write() => throw new NotSupportedException();
    public IFileInfo GetDocumentFile(string fileName) => throw new NotSupportedException();
}
