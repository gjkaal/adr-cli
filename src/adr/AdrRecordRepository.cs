using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace adr
{
    public class AdrRecordRepository : IAdrRecordRepository
    {
        private const string DefaultConsequences = "See https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions for mor information about ADR's.";

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
        private readonly IAdrSettings settings;
        public AdrRecordRepository(
            IAdrSettings settings,
            ILogger<AdrRecordRepository> logger)
        {
            this.settings = settings;
            this.logger = logger;
            logger.LogInformation("AdrRecordRepository Initialization complete");
        }

        public JsonSerializerSettings SerializerSettings => _serializerSettings;
        public async Task<StringBuilder> GetLayoutAsync(AdrRecord record)
        {
            logger.LogInformation($"Retrieving layout for {record.TemplateType}");
            var sb = await GetTemplateAsync(record.TemplateType.ToString());

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
            logger.LogInformation($"Write content layout for {record.Title}");

            IFileInfo metaRecord = settings.GetMetaFile(record.FileName);
            using (var metaWriter = metaRecord.CreateText())
            {
                var meta = record.GetRecord(_serializerSettings);
                await metaWriter.WriteAsync(meta);
                await metaWriter.FlushAsync();
            }
            logger.LogInformation($"Write metadata layout for {record.Title}");
        }

        private async Task<StringBuilder> GetTemplateAsync(string templateName)
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
                    using (var templateContent = templateFile.OpenText())
                    {
                        template = await templateContent.ReadToEndAsync();
                    }
                }
                else
                {
                    template = DefaultTemplate;
                }
            }
            return new StringBuilder(template);
        }
    }
}