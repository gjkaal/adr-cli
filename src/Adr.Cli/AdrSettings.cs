using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Adr.Cli.Extensions;
using Adr.Cli.Sync;

namespace Adr.Cli
{
    public class AdrSettings : IAdrSettings
    {
        private const string DefaultFileName = "adr.config.json";
        private const string DefaultTemplatePath = "\\docs\\adr-templates";
        private const string DefaultAdrPath = "\\docs\\adr";
        private const string DefaultTasksPath = "\\docs\\planning";

        private readonly IPath path;
        private readonly IDirectory directoryService;
        private readonly IFileInfoFactory fileInfoFactory;
        private readonly IDirectoryInfoFactory directoryInfoFactory;
        private string currentPath;
        private string? resolvedConfigFilePath;

        public AdrSettings(IFileSystem fs)
        {
            path = fs.Path;
            fileInfoFactory = fs.FileInfo;
            directoryInfoFactory = fs.DirectoryInfo;
            directoryService = fs.Directory;
            currentPath = directoryService.GetCurrentDirectory();
            Read(this);
        }

        /// <summary>
        /// Return the current command shell folder.
        /// </summary>
        public string CurrentPath => currentPath;

        /// <summary>
        /// If no document folder is provided, this is the path that is used.
        /// </summary>
        public string DefaultDocFolder => DefaultAdrPath;

        /// <summary>
        /// If no project folder is provided, this is the path that is used.
        /// </summary>
        public string DefaultTasksFolder => DefaultTasksPath;

        /// <summary>
        /// If no template folder is provided, this is the path that is used.
        /// </summary>
        public string DefaultTemplates => DefaultTemplatePath;

        /// <summary>
        /// Location where the Adr records and the markdown files will be stored.
        /// </summary>
        public string DocFolder { get; set; } = DefaultAdrPath;

        /// <summary>
        /// Location where the project planning records and the markdown files will be stored.
        /// </summary>
        public string TasksFolder { get; set; } = DefaultTasksPath;

        /// <summary>
        /// Location for markdown templates.
        /// </summary>
        public string TemplateFolder { get; set; } = DefaultTemplatePath;

        /// <summary>
        /// A project name for auto generated content.
        /// </summary>
        public string ProjectName { get; set; } = "ADR Documentation";

        /// <summary>
        /// Configuration for the optional AI provider used to draft ADR proposals.
        /// </summary>
        public AiProviderSettings AiSettings { get; set; } = new();

        /// <summary>
        /// Configuration for the optional task sync connector.
        /// </summary>
        public TaskSyncProviderSettings SyncSettings { get; set; } = new();

        /// <summary>
        /// Describes which adr.config.json is currently active for this process.
        /// </summary>
        public AdrContextInfo CurrentContext => new()
        {
            Success = true,
            ProjectName = ProjectName,
            ConfigFilePath = resolvedConfigFilePath,
            CurrentPath = currentPath,
            DocFolder = DocFolder,
            TasksFolder = TasksFolder,
            TemplateFolder = TemplateFolder,
            AiConfigured = !string.IsNullOrWhiteSpace(AiSettings.Provider),
            AiProvider = AiSettings.Provider,
            SyncConfigured = !string.IsNullOrWhiteSpace(SyncSettings.Provider),
            SyncProvider = SyncSettings.Provider
        };

        /// <summary>
        /// Re-resolve settings from the adr.config.json found by searching upward from
        /// <paramref name="workingDirectory" />. Never creates a config file or ADR folders - if
        /// none is found, existing settings are left untouched and a failure is returned.
        /// </summary>
        public AdrContextInfo TrySetContext(string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return new AdrContextInfo { Success = false, ErrorMessage = "A working directory is required." };
            }

            var directory = directoryInfoFactory.New(workingDirectory);
            if (!directory.Exists)
            {
                return new AdrContextInfo { Success = false, ErrorMessage = $"Directory does not exist: {workingDirectory}" };
            }

            var findPath = directory.FullName;
            while (true)
            {
                var candidatePath = path.Combine(findPath, DefaultFileName);
                var candidate = fileInfoFactory.New(candidatePath);
                if (candidate.Exists)
                {
                    using var stream = candidate.Open(FileMode.Open);
                    if (JsonSerializer.Deserialize(stream, typeof(AdrSettingsFile), jsonOptions) is not AdrSettingsFile value)
                    {
                        return new AdrContextInfo { Success = false, ErrorMessage = $"Could not parse {candidate.FullName}." };
                    }

                    currentPath = findPath;
                    resolvedConfigFilePath = candidate.FullName;
                    DocFolder = string.IsNullOrEmpty(value.Path) ? DefaultAdrPath : value.Path.Replace('/', '\\');
                    TemplateFolder = string.IsNullOrEmpty(value.Templates) ? DefaultTemplatePath : value.Templates.Replace('/', '\\');
                    TasksFolder = string.IsNullOrEmpty(value.Tasks) ? DefaultTasksPath : value.Tasks.Replace('/', '\\');
                    ProjectName = string.IsNullOrEmpty(value.ProjectName) ? ProjectName : value.ProjectName;
                    AiSettings = ToAiProviderSettings(value.Ai);
                    SyncSettings = ToTaskSyncProviderSettings(value.Sync);

                    return CurrentContext;
                }

                var separatorIndex = findPath.LastIndexOf('\\');
                if (separatorIndex <= 0)
                {
                    break;
                }

                findPath = findPath[..separatorIndex];
            }

            return new AdrContextInfo
            {
                Success = false,
                ErrorMessage = $"No {DefaultFileName} found in '{workingDirectory}' or any parent directory. " +
                    "Run adr_init there first if you want to initialize a new repository."
            };
        }

        /// <summary>
        /// Read the content for an ADR.
        /// </summary>
        /// <param name="fileName">
        /// The base name for an ADR, without path or extensions.
        /// </param>
        /// <returns>
        /// A FileInformation object.
        /// </returns>
        public IFileInfo GetContentFile(DocumentType documentType, string fileName)
        {
            if (documentType == DocumentType.Adr)
            {
                return GetAdrFileInfo(DocFolderInfo(), fileName, "md");
            }

            if (documentType == DocumentType.Task)
            {
                return GetAdrFileInfo(TasksFolderInfo(), fileName, "md");
            }

            throw new NotImplementedException($"{documentType} is not accepted");
        }

        /// <summary>
        /// Read the meta data for an ADR.
        /// </summary>
        /// <param name="fileName">
        /// The base name for an ADR, without path or extensions.
        /// </param>
        /// <returns>
        /// A FileInformation object.
        /// </returns>
        public IFileInfo GetMetaFile(DocumentType documentType, string fileName)
        {
            if (documentType == DocumentType.Adr)
            {
                return GetAdrFileInfo(DocFolderInfo(), fileName, "json");
            }

            if (documentType == DocumentType.Task)
            {
                return GetAdrFileInfo(TasksFolderInfo(), fileName, "json");
            }

            throw new NotImplementedException($"{documentType} is not accepted");
        }

        private IFileInfo GetAdrFileInfo(IDirectoryInfo folderInfo, string fileName, string extension)
        {
            var fullFileName =
            fileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.{extension}";

            var filePath = path.Combine(folderInfo.FullName, fullFileName);
            return fileInfoFactory.New(filePath);
        }

        /// <summary>
        /// Generate the next free file number for an ADR.
        /// </summary>
        /// <returns>
        /// 0 is no ADR's are found, or the next increment in the file numbers.
        /// </returns>
        public int GetNextFileNumber(IDirectoryInfo directoryInfo)
        {
            var fileNumOut = 0;
            var files =
                from file in directoryInfo.GetFiles("*.md", SearchOption.TopDirectoryOnly)
                let fileNum = file.Name.IndexOf('-') > 0 ? file.Name[..file.Name.IndexOf('-')] : "0"
                where int.TryParse(fileNum, out fileNumOut)
                select fileNumOut;
            var maxFileNum = files.Any() ? files.Max() : 0;
            return maxFileNum + 1;
        }

        /// <summary>
        /// Get the directory information for the ADR document folder.
        /// </summary>
        public IDirectoryInfo DocFolderInfo()
        {
            return EnsureFolder(DocFolder);
        }

        public IDirectoryInfo TasksFolderInfo()
        {
            return EnsureFolder(TasksFolder);
        }

        private IDirectoryInfo EnsureFolder(string folderName)
        {
            if (folderName.StartsWith('\\'))
            {
                folderName = folderName[1..];
            }

            var folder = path.Combine(currentPath, folderName);
            var directory = directoryInfoFactory.New(folder);
            if (!directory.Exists)
            {
                directory.Create();
                var initFile = path.Combine(directory.FullName, "Initialized.txt");
                var file = fileInfoFactory.New(initFile);
                using var stream = file.OpenWrite();
                var data = Encoding.UTF8.GetBytes($"Folder initialized on {DateTime.UtcNow} UTC");
                stream.Write(data);
            }

            return directory;
        }

        /// <summary>
        /// Get the directory information for the template folder.
        /// </summary>
        public IDirectoryInfo TemplateFolderInfo()
        {
            if (TemplateFolder.StartsWith('\\'))
            {
                TemplateFolder = TemplateFolder[1..];
            }

            var folder = path.Combine(currentPath, TemplateFolder);
            var directory = directoryInfoFactory.New(folder);
            if (!directory.Exists)
            {
                directory.Create();
            }

            return directory;
        }

        /// <summary>
        /// Get the file information for a template.
        /// </summary>
        /// <param name="templateType">
        /// The template type should be formatted using a controlled set.
        /// </param>
        public IFileInfo GetTemplate(string templateType)
        {
            var folderInfo = TemplateFolderInfo();
            var filePath = path.Combine(folderInfo.FullName, $"{templateType}.md");
            return fileInfoFactory.New(filePath);
        }

        /// <summary>
        /// Write current settings
        /// </summary>
        /// <returns>
        /// </returns>
        public IAdrSettings Write()
        {
            var fileInfoPath = path.Combine(currentPath, DefaultFileName);
            var fileInfo = fileInfoFactory.New(fileInfoPath);

            using (Stream stream = fileInfo.Open(FileMode.OpenOrCreate))
            {
                var value = new AdrSettingsFile
                {
                    Path = DocFolder,
                    Templates = TemplateFolder
                };
                JsonSerializer.Serialize(stream, value, typeof(AdrSettingsFile), jsonOptions);
            }
            return this;
        }

        private class AdrSettingsFile
        {
            public string Path { get; set; } = string.Empty;
            public string Templates { get; set; } = string.Empty;
            public string Tasks { get; set; } = string.Empty;
            public string ProjectName { get; set; } = string.Empty;
            public AiConfigSection? Ai { get; set; }
            public SyncConfigSection? Sync { get; set; }
        }

        private class AiConfigSection
        {
            public string Provider { get; set; } = string.Empty;
            public string Endpoint { get; set; } = string.Empty;
            public string DeploymentName { get; set; } = string.Empty;
            public string ApiKeyName { get; set; } = string.Empty;
        }

        private class SyncConfigSection
        {
            public string Provider { get; set; } = string.Empty;
            public string SyncPatName { get; set; } = string.Empty;
            public JsonElement Settings { get; set; }
        }

        private static AiProviderSettings ToAiProviderSettings(AiConfigSection? section)
        {
            if (section == null)
            {
                return new AiProviderSettings();
            }

            return new AiProviderSettings
            {
                Provider = section.Provider,
                Endpoint = section.Endpoint,
                DeploymentName = section.DeploymentName,
                ApiKeyName = section.ApiKeyName
            };
        }

        private static TaskSyncProviderSettings ToTaskSyncProviderSettings(SyncConfigSection? section)
        {
            if (section == null)
            {
                return new TaskSyncProviderSettings();
            }

            return new TaskSyncProviderSettings
            {
                Provider = section.Provider,
                SyncPatName = section.SyncPatName,
                Settings = section.Settings
            };
        }

        private readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters = {
               new JsonStringEnumConverter()
            }
        };

        private IFileInfo? GetConfigFileInfo()
        {
            var findPath = currentPath;
            do
            {
                var fileInfoPath = path.Combine(findPath, DefaultFileName);
                var fileInfo = fileInfoFactory.New(fileInfoPath);
                if (fileInfo.Exists)
                {
                    currentPath = findPath;
                    resolvedConfigFilePath = fileInfo.FullName;
                    return fileInfo;
                }

                findPath = findPath[..findPath.LastIndexOf('\\')];
                if (findPath.LastIndexOf('\\') == -1)
                {
                    findPath = string.Empty;
                    // last resort, use system folder and use current folder as reference
                    currentPath = directoryService.GetCurrentDirectory();
                    var appPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    fileInfoPath = path.Combine(appPath, DefaultFileName);
                    fileInfo = fileInfoFactory.New(fileInfoPath);
                    if (fileInfo.Exists)
                    {
                        resolvedConfigFilePath = fileInfo.FullName;
                        return fileInfo;
                    }
                }
            } while (!string.IsNullOrEmpty(findPath));

            return null;
        }

        private AdrSettings Read(AdrSettings settings)
        {
            var fileInfo = GetConfigFileInfo();
            if (fileInfo == null || !fileInfo.Exists)
            {
                settings.DocFolder = DefaultAdrPath;
                settings.TemplateFolder = DefaultTemplatePath;
                return settings;
            }

            using var stream = fileInfo.Open(FileMode.Open);
            if (JsonSerializer.Deserialize(stream, typeof(AdrSettingsFile), jsonOptions) is AdrSettingsFile value)
            {
                settings.DocFolder = string.IsNullOrEmpty(value.Path) ? settings.DocFolder : value.Path.Replace('/', '\\');
                settings.TemplateFolder = string.IsNullOrEmpty(value.Templates) ? settings.TemplateFolder : value.Templates.Replace('/', '\\');
                settings.TasksFolder = string.IsNullOrEmpty(value.Tasks) ? settings.TasksFolder : value.Tasks.Replace('/', '\\');
                settings.ProjectName = string.IsNullOrEmpty(value.ProjectName) ? settings.ProjectName : value.ProjectName;
                settings.AiSettings = ToAiProviderSettings(value.Ai);
                settings.SyncSettings = ToTaskSyncProviderSettings(value.Sync);
            }

            return settings;
        }

        public bool RepositoryInitialized()
        {
            try
            {
                var folder = DocFolderInfo();
                return folder.EnumerateFiles("*.md").Any();
            }
            catch
            {
                return false;
            }
        }

        public bool TasksInitialized()
        {
            try
            {
                var folder = TasksFolderInfo();
                return folder.Exists;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the folder information for the ADR project root.
        /// </summary>
        public IDirectoryInfo RootFolderInfo()
        {
            var directory = directoryInfoFactory.New(CurrentPath);
            if (!directory.Exists)
            {
                directory.Create();
            }

            return directory;
        }

        /// <summary>
        /// Get a file info object in the project root folder.
        /// </summary>
        /// <param name="fileName">
        /// A filename without path information.
        /// </param>
        /// <returns>
        /// A <see cref="IFileInfo" /> class.
        /// </returns>
        public IFileInfo GetDocumentFile(string fileName)
        {
            var sanitizedFile = fileName.SanitizeFileName();
            var folderInfo = DocFolderInfo().Parent?.FullName;
            folderInfo ??= RootFolderInfo().FullName;
            var filePath = path.Combine(folderInfo, sanitizedFile);
            return fileInfoFactory.New(filePath);
        }
    }
}