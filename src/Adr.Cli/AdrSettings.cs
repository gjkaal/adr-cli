using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Adr.Cli.Extensions;

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
            if (TemplateFolder.StartsWith("\\"))
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
            }

            return settings;
        }

        public bool RepositoryInitialized()
        {
            try
            {
                var folder = DocFolderInfo();
                return folder.Exists;
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