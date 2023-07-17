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
        /// Location where the Adr records and the markdown files will be stored.
        /// </summary>
        public string DocFolder { get; set; } = "\\adr\\doc";

        /// <summary>
        /// Location for markdown templates.
        /// </summary>
        public string TemplateFolder { get; set; } = "\\adr\\templates";

        public IFileInfo GetContentFile(string fileName) => GetAdrFileInfo(fileName, "md");
        public IFileInfo GetMetaFile(string fileName) => GetAdrFileInfo(fileName, "json");

        private IFileInfo GetAdrFileInfo(string fileName, string extension)
        {
            var folderInfo = DocFolderInfo();
            var filePath = path.Combine(folderInfo.FullName, $"{fileName}.{extension}");
            return fileInfoFactory.New(filePath);
        }

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

        private IDirectoryInfo DocFolderInfo()
        {
            var folder = path.Combine(currentPath, DocFolder);
            return directoryInfoFactory.New(folder);
        }

        private IDirectoryInfo TemplateFolderInfo()
        {
            var folder = path.Combine(currentPath, TemplateFolder);
            return directoryInfoFactory.New(folder);
        }

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
                settings.DocFolder = "docs\\adr";
                settings.TemplateFolder = "docs\\adr\\templates";
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
            if (!docFolder.Exists)
            {
                // created but not initalized
                docFolder.Create();
                return false;
            }
            return docFolder.EnumerateFiles().Any();
        }
    }
}