using Adr.Cli.Extensions;
using Adr.Cli.Services;

using Microsoft.Extensions.Logging;

using System;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adr.Cli
{
    public class AdrRecordRepository : DocumentBasedRepository, IAdrRecordRepository
    {
        // These are rendered whenever a record's Context/Decision/Consequences is empty at write time
        // (no AI draft, no manual input) - deliberately unmistakable as placeholder text rather than
        // plausible-sounding prose, so an unfilled ADR can't be mistaken for a finished one.
        private const string DefaultConsequences =
            "*(Consequences not yet documented - replace this placeholder with the resulting trade-offs, risks, and follow-up actions.)*";

        private const string DefaultContext =
            "*(Context not yet documented - replace this placeholder with the situation and forces behind this decision.)*";

        private const string DefaultDecision =
            "*(Decision not yet documented - replace this placeholder with the decision that was made and its rationale.)*";

        private const string defaultTemplate = @"# {RecordId}. {Title}

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

        public AdrRecordRepository(
            IFileSystem fileSystem,
            IAdrSettings settings,
            IStdOut stdOut,
            IFileLock fileLock,
            ILogger<AdrRecordRepository> logger) : base(fileSystem, settings, stdOut, fileLock, logger)
        {
            logger.LogDebug("AdrRecordRepository Initialization complete");
            logger.LogDebug("Documents located in {DocFolderInfo}", settings.DocFolderInfo().FullName);
            logger.LogDebug("Templates located in {TemplateFolderInfo}", settings.TemplateFolderInfo().FullName);
        }

        protected override IDirectoryInfo BaseFolder => settings.DocFolderInfo();
        protected override string DefaultTemplate => defaultTemplate;

        public async Task<AdrRecord> CopyRecordAsync(AdrRecord record, int newId, bool isRevision)
        {
            using (await fileLock.AcquireLockAsync(BaseFolder.FullName, "CopyRecord"))
            {
                var newRecord = (AdrRecord)record.Clone();
                newRecord.RecordId = newId;
                newRecord.Status = AdrStatus.New;
                if (isRevision)
                {
                    newRecord.SuperSedes = record;
                }
                newRecord.PrepareForStorage();
                var metaRecord = settings.GetMetaFile(DocumentType.Adr, newRecord.FileName);
                using (var metaWriter = metaRecord.CreateText())
                {
                    var meta = newRecord.GetMetadata(Constants.JsonOptions);
                    await metaWriter.WriteAsync(meta);
                    await metaWriter.FlushAsync();
                }
                logger.LogDebug("Write metadata for {Title}", newRecord.Title);

                logger.LogInformation("Write ADR #{RecordId} to {FileName}", newRecord.RecordId, newRecord.FileName);
                var newContent = await ReadContentAsync(record.RecordId);
                newContent[0] = $"# {newId:D5}: {newRecord.Title}";
                newContent = newContent.ReplaceMdContent("Status", [$"__{newRecord.Status}__"]).ToArray();

                var contentRecord = settings.GetContentFile(DocumentType.Adr, newRecord.FileName);
                using (var contentWriter = contentRecord.CreateText())
                {
                    foreach (var line in newContent)
                    {
                        await contentWriter.WriteLineAsync(line);
                    }
                    await contentWriter.FlushAsync();
                }

                return newRecord;
            }
        }

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
        /// Read the Adr metadata from an existing record. If more than one match exists, then the
        /// first match is used.
        /// </summary>
        /// <param name="recordId">
        /// A valid, positive integer value.
        /// </param>
        /// <returns>
        /// null if no metadata record exists, or an <see cref="AdrRecord" /> with the metadata
        /// (without the content fields)
        /// </returns>
        public async Task<AdrRecord?> ReadMetadataAsync(int recordId)
        {
            var file = GetFileInfoForRecord(recordId, AdrFileType.Json);
            if (file == null) { return null; }
            var result = await ReadAdrFromFile(recordId, file);
            return result;
        }

        public Task<int> UpdateContentAsync(AdrRecord record, string[] lines)
        {
            return UpdateFileContentAsync(record, lines);
        }

        /// <summary>
        /// Update the metadata file for the specified record.
        /// </summary>
        /// <param name="recordId">
        /// A numeric value.
        /// </param>
        /// <param name="record">
        /// The content for the metadata file.
        /// </param>
        /// <returns>
        /// </returns>
        public Task<int> UpdateMetadataAsync(int recordId, AdrRecord record)
        {
            return UpdateMetadataRecordAsync(recordId, record);
        }

        /// <summary>
        /// Write a Markdown file containing the information for the ADR.
        /// </summary>
        /// <param name="record">
        /// The ADR information.
        /// </param>
        /// <returns>
        /// The (newly created) record identifier.
        /// </returns>
        public Task<int> WriteRecordAsync(AdrRecord record)
        {
            return WriteRecordAsync(
                record,
                AdrRecordExtensions.Validate,
                AdrRecordExtensions.PrepareForStorage,
                GetLayoutAsync
                );
        }

        private Task<AdrRecord> ReadAdrFromFile(int recordId, IFileInfo fileInfo)
        {
            return ReadFromFile<AdrRecord>(recordId, fileInfo);
        }
    }
}