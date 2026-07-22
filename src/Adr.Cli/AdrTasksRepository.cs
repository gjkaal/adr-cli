using Adr.Cli.Extensions;
using Adr.Cli.Services;

using Microsoft.Extensions.Logging;

using System;
using System.IO.Abstractions;
using System.Text;
using System.Threading.Tasks;

namespace Adr.Cli;

public class AdrTasksRepository : DocumentBasedRepository, IAdrTasksRepository
{
    private const string DefaultTaskDescription = "New task.";

    private const string DefaultPrerequisits = "No prerequisits.";

    private const string defaultTemplate = @"# {RecordId}. {Title}

{DateTime}

## Status

{Status}

## Description

{Description}

## Prerequisits

{Prerequisits}

## Details

{Details}

{Related}
";

    public AdrTasksRepository(
        IFileSystem fileSystem,
        IAdrSettings settings,
        IStdOut stdOut,
        IFileLock fileLock,
        ILogger<AdrTasksRepository> logger) : base(fileSystem, settings, stdOut, fileLock, logger)
    {
        logger.LogDebug("AdrTasksRepository Initialization complete");
        logger.LogDebug("Tasks located in {TasksFolderInfo}", settings.TasksFolderInfo().FullName);
        logger.LogDebug("Templates located in {TemplateFolderInfo}", settings.TemplateFolderInfo().FullName);
    }

    protected override IDirectoryInfo BaseFolder => settings.TasksFolderInfo();
    protected override string DefaultTemplate => defaultTemplate;

    public async Task<StringBuilder> GetLayoutAsync(TaskRecord record)
    {
        logger.LogInformation($"Retrieving layout for {TemplateType.Task}");
        var sb = await GetOrCreateTemplateAsync(TemplateType.Task.ToString());

        sb.Replace("{RecordId}", record.RecordId.ToString("D5"));
        sb.Replace("{Title}", record.Title);
        sb.Replace("{Status}", $"__{record.Status}__");
        sb.Replace("{Description}", string.IsNullOrEmpty(record.Description) ? DefaultTaskDescription : record.Description);
        sb.Replace("{Prerequisits}", string.IsNullOrEmpty(record.Prerequisits) ? DefaultPrerequisits : record.Prerequisits);
        sb.Replace("{Details}", string.IsNullOrEmpty(record.Details) ? DefaultTaskDescription : record.Details);
        sb.Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd"));

        if (record.Related == null || record.Related.Count == 0)
        {
            sb.Replace("{Related}", string.Empty);
        }
        else
        {
            var relatedBlock = new StringBuilder();
            relatedBlock.AppendLine();
            relatedBlock.AppendLine();
            relatedBlock.AppendLine("## Related tasks");
            relatedBlock.AppendLine();
            relatedBlock.AppendLine();

            foreach (var item in record.Related)
            {
                var fileName = GetFileInfoForRecord(item.Key, AdrFileType.Md);
                if (fileName != null)
                {
                    relatedBlock.AppendLine($"[{item.Key:D5} {item.Value}](./{fileName.Name})");
                }
            }
            sb.Replace("{Related}", relatedBlock.ToString());
        }

        return sb;
    }

    public async Task<TaskRecord?> ReadMetadataAsync(int recordId)
    {
        var file = GetFileInfoForRecord(recordId, AdrFileType.Json);
        if (file == null) { return null; }
        var result = await ReadTaskFromFile(recordId, file);
        return result;
    }

    public Task<int> UpdateContentAsync(TaskRecord record, string[] lines)
    {
        return UpdateFileContentAsync(record, lines);
    }

    public Task<int> UpdateMetadataAsync(int recordId, TaskRecord record)
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
    public Task<int> WriteRecordAsync(TaskRecord record)
    {
        return WriteRecordAsync(
            record,
            AdrRecordExtensions.Validate,
            AdrRecordExtensions.PrepareForStorage,
            GetLayoutAsync
            );
    }

    private Task<TaskRecord> ReadTaskFromFile(int recordId, IFileInfo fileInfo)
    {
        return ReadFromFile<TaskRecord>(recordId, fileInfo);
    }
}