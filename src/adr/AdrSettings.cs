using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace adr
{
    public class AdrSettings : IAdrSettings
    {
        private const string DefaultFileName = "adr.config.json";
        private const string DefaultTemplateFolder = "\\docs\\adr-templates";
        private const string DefaultAdrFolder = "\\docs\\adr";
        private readonly IPath path;
        private readonly IFileInfoFactory fileInfoFactory;
        private readonly IDirectoryInfoFactory directoryInfoFactory;
        private readonly string currentPath;

        public AdrSettings(IFileSystem fs)
        {
            path = fs.Path;
            fileInfoFactory = fs.FileInfo;
            directoryInfoFactory = fs.DirectoryInfo;
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            currentPath = assemblyLocation.Substring(0, assemblyLocation.LastIndexOfAny(new[] { '/', '\\' }));
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
            var folderInfo = DocFolderInfo();
            var filePath = path.Combine(folderInfo.FullName, $"{fileName}.{extension}");
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
            var fileInfo = GetConfigFileInfo();
            
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

        private IFileInfo GetConfigFileInfo()
        {
            var fileInfoPath = path.Combine(currentPath, DefaultFileName);
            var fileInfo = fileInfoFactory.New(fileInfoPath);
            return fileInfo;
        }

        private AdrSettings Read(AdrSettings settings)
        {
            var fileInfo = GetConfigFileInfo();
            if (!fileInfo.Exists)
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

                var value = (dynamic)serializer.Deserialize(stream, new { path = "", template = "" }.GetType());
                settings.DocFolder = value.path;
                settings.TemplateFolder = value.template;
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