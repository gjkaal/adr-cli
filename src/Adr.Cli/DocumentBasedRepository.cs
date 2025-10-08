using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Adr.Cli.Extensions;
using Adr.Cli.Services;

using Microsoft.Extensions.Logging;

namespace Adr.Cli;

public abstract class DocumentBasedRepository
{
    protected readonly IFileSystem fileSystem;
    protected readonly ILogger logger;
    protected readonly IAdrSettings settings;
    protected readonly IStdOut stdOut;

    protected DocumentBasedRepository(
        IFileSystem fileSystem,
            IAdrSettings settings,
            IStdOut stdOut,
            ILogger logger)
    {
        this.fileSystem = fileSystem;
        this.settings = settings;
        this.stdOut = stdOut;
        this.logger = logger;
    }

    protected abstract IDirectoryInfo BaseFolder { get; }
    protected abstract string DefaultTemplate { get; }

    public async Task<(bool success, string fullFilePath)> CreateRootDocumentAsync(string fileName, StringBuilder fileContent)
    {
        var doc = settings.GetDocumentFile(fileName);
        IFileInfo? backupDoc = null;
        var success = false;
        try
        {
            if (doc.Exists)
            {
                var backupFile = doc.FullName + ".back";
                if (fileSystem.File.Exists(backupFile))
                {
                    fileSystem.File.Delete(backupFile);
                }
                backupDoc = doc.CopyTo(backupFile);
            }
            using (var writer = doc.CreateText())
            {
                await writer.WriteAsync(fileContent);
                writer.Flush();
            }
            success = true;
        }
        finally
        {
            if (success && backupDoc != null)
            {
                backupDoc.Delete();
            }
        }
        return new(success, doc.FullName);
    }

    /// <summary>
    /// Read the content file
    /// </summary>
    /// <param name="recordId">
    /// </param>
    /// <returns>
    /// </returns>
    public async Task<string[]> ReadContentAsync(int recordId)
    {
        var file = GetFileInfoForRecord(recordId);

        var contentLines = new List<string>();
        var content = string.Empty;
        if (file == null) { return []; }
        using (var markdownContent = file.OpenText())
        {
            content = await markdownContent.ReadToEndAsync();
        }

        contentLines.AddRange(content.Split(Environment.NewLine));

        return [.. contentLines];
    }

    protected IFileInfo? GetFileInfoForRecord(int recordId)
    {
        var adrDocumentFolder = BaseFolder;
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
        return files[0];
    }

    protected async Task<StringBuilder> GetOrCreateTemplateAsync(string templateName)
    {
        string template;
        if (string.IsNullOrEmpty(templateName))
        {
            template = DefaultTemplate;
        }
        else
        {
            var templateFile = settings.GetTemplate(templateName);
            if (templateFile.Exists)
            {
                logger.LogDebug("Reading template file '{TemplateName}'", templateName);
                using var templateContent = templateFile.OpenText();
                template = await templateContent.ReadToEndAsync();
            }
            else
            {
                logger.LogInformation("Create new template file for '{TemplateName}'", templateName);
                template = DefaultTemplate;
                using var templateWriter = templateFile.CreateText();
                await templateWriter.WriteAsync(template);
                await templateWriter.FlushAsync();
            }
        }
        return new StringBuilder(template);
    }

    protected async Task<T> ReadFromFile<T>(int recordId, IFileInfo fileInfo) where T : AdrRecordBase, new()
    {
        if (!fileInfo.Exists)
        {
            return new T
            {
                RecordId = recordId,
                DateTime = DateTime.Now,
                FileName = fileInfo.FullName,
                Title = "File not found"
            };
        }

        using var metadataContent = fileInfo.OpenText();
        var content = await metadataContent.ReadToEndAsync();
        try
        {
            var record = JsonSerializer.Deserialize<T>(content, Constants.JsonOptions);
            if (record!.RecordId != recordId)
            {
                stdOut.WriteLine($"{fileInfo.Name} contains invalid record id : {record.RecordId}");
            }

            return record;
        }
        catch (Exception e)
        {
            var fileDate = fileSystem.File.GetCreationTime(fileInfo.FullName);
            return new T
            {
                RecordId = recordId,
                DateTime = fileDate,
                FileName = fileInfo.FullName,
                Title = e.Message
            };
        }
    }

    protected async Task<int> UpdateFileContentAsync<T>(T record, string[] lines) where T : AdrRecordBase
    {
        var documentType = DocumentTypeForRecord(record);

        logger.LogInformation("Update #{RecordId} with new content.", record.RecordId);
        var contextRecord = settings.GetContentFile(documentType, record.FileName);
        var backupFileName = record.FileName + ".bak";
        var contextBackup = settings.GetContentFile(documentType, backupFileName);
        if (!contextRecord.Exists)
        {
            return -1;
        }

        contextRecord.CopyTo(backupFileName, true);
        var contentLength = 0;
        var charactersWritten = 0;
        using (var contentWriter = contextRecord.CreateText())
        {
            foreach (var line in lines)
            {
                await contentWriter.WriteLineAsync(line);
                contentLength += line.Length;
            }
            await contentWriter.FlushAsync();
            charactersWritten = contentLength;
        }
        logger.LogDebug("Update content for {Title}", record.Title);
        if (contextBackup.Exists)
        {
            contextBackup.Delete();
        }

        return charactersWritten;
    }

    private static DocumentType DocumentTypeForRecord<T>(T record) where T : AdrRecordBase
    {
        var documentType = DocumentType.None;
        if (record is AdrRecord)
        {
            documentType = DocumentType.Adr;
        }

        if (record is TaskRecord)
        {
            documentType = DocumentType.Task;
        }

        return documentType;
    }

    protected async Task<int> UpdateMetadataRecordAsync<T>(int recordId, T record) where T : AdrRecordBase
    {
        var documentType = DocumentTypeForRecord(record);
        record.RecordId = recordId;
        logger.LogInformation("Update #{RecordId} to {FileName}", record.RecordId, record.FileName);
        var metaRecord = settings.GetMetaFile(documentType, record.FileName);
        if (!metaRecord.Exists)
        {
            return -1;
        }

        var bytesWritten = 0;
        using (var metaWriter = metaRecord.CreateText())
        {
            var meta = record.GetMetadata(Constants.JsonOptions);
            await metaWriter.WriteAsync(meta);
            await metaWriter.FlushAsync();
            bytesWritten = meta.Length;
        }
        logger.LogDebug("Update metadata for {Title}", record.Title);
        return bytesWritten;
    }

    protected async Task<int> WriteRecordAsync<T>(
                                    T record,
        Action<T> validate,
        Func<T, T> prepareForStorage,
        Func<T, Task<StringBuilder>> getLayoutAsync
        ) where T : AdrRecordBase
    {
        record.RecordId = settings.GetNextFileNumber(BaseFolder);
        validate.Invoke(record);
        record = prepareForStorage.Invoke(record);

        logger.LogInformation("Write #{RecordId} to {FileName}", record.RecordId, record.FileName);

        // Use BaseFolder instead of settings methods to support both ADR and Tasks folders
        var contentFilePath = fileSystem.Path.Combine(BaseFolder.FullName, $"{record.FileName}.md");
        var contentRecord = fileSystem.FileInfo.New(contentFilePath);
        using (var contentWriter = contentRecord.CreateText())
        {
            var content = await getLayoutAsync.Invoke(record);
            await contentWriter.WriteAsync(content);
            await contentWriter.FlushAsync();
        }
        logger.LogDebug("Write content for {RecordType} {Title}", record.GetType().Name, record.Title);

        var metaFilePath = fileSystem.Path.Combine(BaseFolder.FullName, $"{record.FileName}.json");
        var metaRecord = fileSystem.FileInfo.New(metaFilePath);
        using (var metaWriter = metaRecord.CreateText())
        {
            var meta = record.GetMetadata(Constants.JsonOptions);
            await metaWriter.WriteAsync(meta);
            await metaWriter.FlushAsync();
        }
        logger.LogDebug("Write metadata for {Title}", record.Title);

        return 1;
    }
}