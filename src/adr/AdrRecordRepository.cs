using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using adr.Extensions;
using System.IO;
using adr.Services;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace adr
{
    public class AdrRecordRepository : IAdrRecordRepository
    {
        private const string DefaultConsequences = "See https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions for more information about ADR's.";

        private const string DefaultContext = "Architecture for agile projects has to be described and defined differently. Not all decisions will be made at once, nor will all of them be done when the project begins.";

        private const string DefaultDecision = "We will keep a collection of records for \"architecturally significant\" decisions: those that affect the structure, non-functional characteristics, dependencies, interfaces, or construction techniques.";

        private const string DefaultTemplate = @"# {RecordId}. {Title}

{DateTime}{Supersedes}

## Status

{Status}

## Context

{Context}

## Decision

{Decision}

## Consequences

{Consequences}
";
        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Culture = CultureInfo.InvariantCulture,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,            
            Converters = {
               new StringEnumConverter()
            }
        };

        private readonly IFileSystem fileSystem;
        private readonly ILogger<AdrRecordRepository> logger;
        private readonly IAdrSettings settings;
        private readonly IStdOut stdOut;

        public AdrRecordRepository(
            IFileSystem fileSystem,
            IAdrSettings settings,
            IStdOut stdOut,
            ILogger<AdrRecordRepository> logger)
        {
            this.fileSystem = fileSystem;
            this.settings = settings;
            this.stdOut = stdOut;
            this.logger = logger;
            logger.LogDebug("AdrRecordRepository Initialization complete");
            logger.LogDebug($"Documents located in {settings.DocFolderInfo().FullName}");
            logger.LogDebug($"Templates located in {settings.TemplateFolderInfo().FullName}");
        }

        public JsonSerializerSettings SerializerSettings => _serializerSettings;

        public async Task<StringBuilder> GetLayoutAsync(AdrRecord record)
        {
            logger.LogInformation($"Retrieving layout for {record.TemplateType}");
            var sb = await GetOrCreateTemplateAsync(record.TemplateType.ToString());

            sb.Replace("{RecordId}", record.RecordId.ToString("D5"));
            sb.Replace("{Title}", record.Title);
            sb.Replace("{Status}", $"__{record.Status}__");
            sb.Replace("{Context}", string.IsNullOrEmpty(record.Context) ? DefaultContext : record.Context);
            sb.Replace("{Decision}", string.IsNullOrEmpty(record.Decision) ? DefaultDecision : record.Decision);
            sb.Replace("{Consequences}", string.IsNullOrEmpty(record.Consequences) ? DefaultConsequences : record.Consequences);
            sb.Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd"));

            if (record.SuperSedes == null)
            {
                sb.Replace("{Supersedes}", string.Empty);
            }
            else
            {
                var superSedesBlock = new StringBuilder();
                superSedesBlock.AppendLine();
                superSedesBlock.AppendLine();
                superSedesBlock.Append($"__Supersedes:__ [{record.SuperSedes.RecordId:D5} {record.SuperSedes.Title}](./{record.SuperSedes.FileName}.md)");
                sb.Replace("{Supersedes}", superSedesBlock.ToString());
            }

            return sb;
        }

        /// <summary>
        /// Read the Adr metadata from an existing record.
        /// If more than one match exists, then the first match is used.
        /// </summary>
        /// <param name="recordId">A valid, positive integer value.</param>
        /// <returns>null if no metadata record exists, or an <see cref="AdrRecord"/> with the metadata (without the content fields)</returns>
        public async Task<AdrRecord?> ReadMetadataAsync(int recordId)
        {
            var adrDocumentFolder = settings.DocFolderInfo();
            var matchFileName = $"{recordId:D5}-*.json";
            var files = adrDocumentFolder.EnumerateFiles(matchFileName).ToArray();
            if (files.Length == 0)
            {
                return null;
            }
            if (files.Length > 1)
            {
                var fileNames = string.Join(Environment.NewLine, files.Select(m => m.Name));
                stdOut.WriteLine($"Found more than one matching file, selecting the first from:{Environment.NewLine}{fileNames}");
            }

            var result = await ReadAdrFromFile(recordId, files[0]);
            return result;
        }

        /// <summary>
        /// Read the content file
        /// </summary>
        /// <param name="recordId"></param>
        /// <returns></returns>
        public async Task<string[]> ReadContentAsync(int recordId)
        {
            var adrDocumentFolder = settings.DocFolderInfo();
            var matchFileName = $"{recordId:D5}-*.md";
            var files = adrDocumentFolder.EnumerateFiles(matchFileName).ToArray();
            if (files.Length == 0)
            {
                return Array.Empty<string>();
            }
            if (files.Length > 1)
            {
                var fileNames = string.Join(Environment.NewLine, files.Select(m => m.Name));
                stdOut.WriteLine($"Found more than one matching file, selecting the first from:{Environment.NewLine}{fileNames}");
            }

            logger.LogInformation($"Reading from {files[0].FullName}");

            var contentLines = new List<string>();
            string content = string.Empty;
            using (var markdownContent = files[0].OpenText())
            content = await markdownContent.ReadToEndAsync();
            contentLines.AddRange(content.Split(Environment.NewLine));

            return contentLines.ToArray();
        }

        public async Task<int> UpdateContentAsync(AdrRecord record, string[] lines)
        {
            logger.LogInformation($"Update ADR #{record.RecordId} with new content.");
            IFileInfo contextRecord = settings.GetContentFile(record.FileName);
            var backupFileName = record.FileName + ".bak";
            IFileInfo contextBackup = settings.GetContentFile(backupFileName);
            if (!contextRecord.Exists) return -1;

            contextRecord.CopyTo(backupFileName, true);
            var contentLength = 0;
            var charactersWritten = 0;
            using (var contentWriter = contextRecord.CreateText())
            {
                foreach(var line in lines)
                {
                    await contentWriter.WriteLineAsync(line);
                    contentLength += line.Length;
                }
                await contentWriter.FlushAsync();
                charactersWritten = contentLength;
            }
            logger.LogDebug($"Update content for {record.Title}");
            if (contextBackup.Exists) contextBackup.Delete();
            return charactersWritten;
        }

        public async Task<int> UpdateMetadataAsync(int recordId, AdrRecord record)
        {
            record.RecordId = recordId;
            logger.LogInformation($"Update ADR #{record.RecordId} to {record.FileName}");
            IFileInfo metaRecord = settings.GetMetaFile(record.FileName);
            if (!metaRecord.Exists) return -1;

            var bytesWritten = 0;
            using (var metaWriter = metaRecord.CreateText())
            {
                var meta = record.GetMetadata(_serializerSettings);
                await metaWriter.WriteAsync(meta);
                await metaWriter.FlushAsync();
                bytesWritten = meta.Length;
            }
            logger.LogDebug($"Update metadata for {record.Title}");
            return bytesWritten;
        }

        public async Task WriteRecordAsync(AdrRecord record)
        {
            record.RecordId = settings.GetNextFileNumber();
            record.Validate();
            record.PrepareForStorage();
            
            logger.LogInformation($"Write ADR #{record.RecordId} to {record.FileName}");

            IFileInfo contentRecord = settings.GetContentFile(record.FileName);
            using (var contentWriter = contentRecord.CreateText())
            {
                var content = await GetLayoutAsync(record);
                await contentWriter.WriteAsync(content);
                await contentWriter.FlushAsync();
            }
            logger.LogDebug($"Write content for {record.Title}");

            IFileInfo metaRecord = settings.GetMetaFile(record.FileName);
            using (var metaWriter = metaRecord.CreateText())
            {
                var meta = record.GetMetadata(_serializerSettings);
                await metaWriter.WriteAsync(meta);
                await metaWriter.FlushAsync();
            }
            logger.LogDebug($"Write metadata for {record.Title}");
        }

        private async Task<StringBuilder> GetOrCreateTemplateAsync(string templateName)
        {
            string template;
            if (string.IsNullOrEmpty(templateName))
            {
                template = DefaultTemplate;
            }
            else
            {
                IFileInfo templateFile = settings.GetTemplate(templateName);
                if (templateFile.Exists)
                {
                    logger.LogDebug($"Reading template file '{templateName}'");
                    using (var templateContent = templateFile.OpenText())
                    {
                        template = await templateContent.ReadToEndAsync();
                    }
                }
                else
                {

                    logger.LogInformation($"Create new template file for '{templateName}'");
                    template = DefaultTemplate;
                    using (var templateWriter = templateFile.CreateText())
                    {
                        await templateWriter.WriteAsync(template);
                        await templateWriter.FlushAsync();
                    }
                }
            }
            return new StringBuilder(template);
        }

        private async Task<AdrRecord> ReadAdrFromFile(int recordId, IFileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return new AdrRecord
                {
                    RecordId = recordId,
                    Status = AdrStatus.Error,
                    DateTime = DateTime.Now,
                    FileName = fileInfo.FullName,
                    Title = "File not found"
                };
            }

            using (var metadataContent = fileInfo.OpenText())
            {
                var content = await metadataContent.ReadToEndAsync();
                try
                {
                    var record = JsonConvert.DeserializeObject<AdrRecord>(content, SerializerSettings);
                    if(record.RecordId != recordId)
                    {
                        stdOut.WriteLine($"{fileInfo.Name} contains invalid record id : {record.RecordId}");                        
                    }
                    
                    return record;
                }
                catch (Exception e)
                {
                    var fileDate = fileSystem.File.GetCreationTime(fileInfo.FullName);
                    return new AdrRecord
                    {
                        RecordId = recordId,
                        Status = AdrStatus.Error,
                        DateTime = fileDate,
                        FileName = fileInfo.FullName,
                        Title = e.Message
                    };
                }
            }
        }
    }
}