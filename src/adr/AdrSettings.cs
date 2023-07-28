using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Newtonsoft.Json;

namespace adr
{
    public class AdrSettings : IAdrSettings
    {
        private const string DefaultFileName = "adr.config.json";
        private const string DefaultTemplateFolder = "\\docs\\adr-templates";
        private const string DefaultAdrFolder = "\\docs\\adr";
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
        /// If no documentfolder is provided, this is the path that is used.
        /// </summary>
        public string DefaultDocFolder => DefaultAdrFolder;

        /// <summary>
        /// If no template folder is provided, this is the path that is used.
        /// </summary>
        public string DefaultTemplates => DefaultTemplateFolder;

        /// <summary>
        /// Location where the Adr records and the markdown files will be stored.
        /// </summary>
        public string DocFolder { get; set; } = DefaultAdrFolder;

        /// <summary>
        /// Location for markdown templates.
        /// </summary>
        public string TemplateFolder { get; set; } = DefaultTemplateFolder;

        /// <summary>
        /// Read the content for an ADR.
        /// </summary>
        /// <param name="fileName">The base name for an ADR, without path or extensions.</param>
        /// <returns>A FileInformation object.</returns>
        public IFileInfo GetContentFile(string fileName) => GetAdrFileInfo(fileName, "md");

        /// <summary>
        /// Read the meta data for an ADR.
        /// </summary>
        /// <param name="fileName">The base name for an ADR, without path or extensions.</param>
        /// <returns>A FileInformation object.</returns>
        public IFileInfo GetMetaFile(string fileName) => GetAdrFileInfo(fileName, "json");

        private IFileInfo GetAdrFileInfo(string fileName, string extension)
        {
            var fullFileName = 
            fileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.{extension}";
            
            var folderInfo = DocFolderInfo();
            var filePath = path.Combine(folderInfo.FullName, fullFileName);
            return fileInfoFactory.New(filePath);
        }

        /// <summary>
        /// Generate the next free file number for an ADR.
        /// </summary>
        /// <returns>0 is no ADR's are found, or the next increment in the file numbers.</returns>
        public int GetNextFileNumber()
        {
            var docFolderInfo = DocFolderInfo();

            int fileNumOut = 0;
            var files =
                from file in docFolderInfo.GetFiles("*.md", SearchOption.TopDirectoryOnly)
                let fileNum = file.Name.Substring(0, file.Name.IndexOf('-'))
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
            if (DocFolder.StartsWith("\\")) DocFolder = DocFolder[1..];
            var folder = path.Combine(currentPath, DocFolder);
            var directory = directoryInfoFactory.New(folder);
            if (!directory.Exists) directory.Create();
            return directory;        
        }

        /// <summary>
        /// Get the directory information for the template folder.
        /// </summary>
        public IDirectoryInfo TemplateFolderInfo()
        {
            if (TemplateFolder.StartsWith("\\")) TemplateFolder = TemplateFolder[1..];
            var folder = path.Combine(currentPath, TemplateFolder);
            var directory = directoryInfoFactory.New(folder);
            if (!directory.Exists) directory.Create();
            return directory;
        }

        /// <summary>
        /// Get the file information for a template.
        /// </summary>
        /// <param name="templateType">The template type should be formatted using a controlled set.</param>
        public IFileInfo GetTemplate(string templateType)
        {
            var folderInfo = TemplateFolderInfo();
            var filePath = path.Combine(folderInfo.FullName, $"{templateType}.md");
            return fileInfoFactory.New(filePath);
        }

        /// <summary>
        /// Write current settings
        /// </summary>
        /// <returns></returns>
        public IAdrSettings Write()
        {
            var fileInfoPath = path.Combine(currentPath, DefaultFileName);
            var fileInfo = fileInfoFactory.New(fileInfoPath);

            using (var stream = fileInfo.CreateText())
            {
                var value = new
                {
                    path = DocFolder,
                    templates = TemplateFolder
                };
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };
                serializer.Serialize(stream, value);
            }

            return this;
        }

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
                    // last resort, use system folder
                    // and use current folder as reference
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
                settings.DocFolder = DefaultAdrFolder;
                settings.TemplateFolder = DefaultTemplateFolder;
                return settings;
            }

            using (var stream = fileInfo.OpenText())
            {
                var serializer = new JsonSerializer
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };

                var value = (dynamic)serializer.Deserialize(stream, new { path = "", templates = "" }.GetType());
                settings.DocFolder = string.IsNullOrEmpty(value.path) ? settings.DocFolder : value.path;
                settings.TemplateFolder = string.IsNullOrEmpty(value.templates) ? settings.TemplateFolder : value.templates;
                return settings;
            }
        }

        public bool RepositoryInitialized()
        {
            var docFolder = DocFolderInfo();            
            return docFolder.EnumerateFiles().Any();
        }
    }
}