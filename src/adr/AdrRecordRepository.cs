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
using Microsoft.VisualBasic;

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

        private readonly ILogger<AdrRecordRepository> logger;
        private readonly IFileSystem fileSystem;
        private readonly IAdrSettings settings;
        public AdrRecordRepository(
            IFileSystem fileSystem,
            IAdrSettings settings,
            ILogger<AdrRecordRepository> logger)
        {
            this.fileSystem = fileSystem;
            this.settings = settings;
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
            sb.Replace("{Status}", record.Status.ToString());
            sb.Replace("{Context}", string.IsNullOrEmpty(record.Context) ? DefaultContext : record.Context);
            sb.Replace("{Decision}", string.IsNullOrEmpty(record.Decision) ? DefaultDecision : record.Decision);
            sb.Replace("{Consequences}", string.IsNullOrEmpty(record.Consequences) ? DefaultConsequences : record.Consequences);
            sb.Replace("{DateTime}", DateTime.Now.ToString("dd-MM-yyyy"));

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
            AdrRecord result;
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
                logger.LogWarning($"Found more than one matching file, selecting the first from:{Environment.NewLine}{fileNames}");
            }

            using (var metadataContent = files[0].OpenText())
            {
                var content = await metadataContent.ReadToEndAsync();
                try
                {
                    result = JsonConvert.DeserializeObject<AdrRecord>(content, SerializerSettings);
                }
                catch(Exception e)
                {
                    var fileDate = fileSystem.File.GetCreationTime(files[0].FullName);
                    result = new AdrRecord
                    {
                        RecordId = recordId,
                        Status = AdrStatus.Error,
                        DateTime = fileDate,
                        Title = e.Message
                    };
                }
            }
            return result;
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
    }
}