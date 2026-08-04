using Adr.Cli.Extensions;
using Adr.Cli.Services;

using McpCore;

using Microsoft.Extensions.Logging;

using System;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for ADR initialization
/// </summary>
public class AdrInit : IAdrInit
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrInit> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IStdOut stdOut;
    private readonly IProcessHelper processHelper;

    /// <summary>
    /// Written verbatim into ADR-00001's Context on a clean init - the template's generic
    /// {Context} placeholder must never be what a new repository's first ADR shows.
    /// </summary>
    private const string InitialContext =
        "Architecture for agile projects has to be described and defined differently. Not all " +
        "decisions will be made at once, nor will all of them be done when the project begins.";

    private const string InitialDecision =
        "We will keep a collection of records for \"architecturally significant\" decisions: those " +
        "that affect the structure, non-functional characteristics, dependencies, interfaces, or " +
        "construction techniques.";

    /// <summary>
    /// Appended to <see cref="InitialDecision" /> only when an AI provider is configured at init
    /// time - documents, in the ADR itself, that AI-assisted drafting is part of this repository's
    /// ADR workflow rather than a silent implementation detail.
    /// </summary>
    private const string AiDraftingDecisionNote =
        " Where an AI provider is configured, a preliminary draft of each new ADR's Context, " +
        "Decision, and Consequences is proposed by an AI agent and must be reviewed before it is " +
        "treated as final.";

    private const string InitialConsequences =
        "See [cognitect 2011.11.15](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) " +
        "for more information about ADR's.\n\n" +
        "This documentation is created using the [adr-cli tool](https://github.com/gjkaal/adr-cli).";

    public AdrInit(
        IAdrSettings settings,
        ILogger<AdrInit> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut,
        IProcessHelper processHelper)
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
        this.processHelper = processHelper;
    }

    /// <summary>
    /// Initialize an ADR, with optionally providing a path where the documents are stored and a
    /// path where the templates can be found. The settings are stored in a config file.
    /// </summary>
    /// <param name="adrRootPath">
    /// An alternate for the document folder, default is '\docs\adr'.
    /// </param>
    /// <param name="templateRootPath">
    /// An alternate for the template folder, default is '\docs\adr\template'
    /// </param>
    /// <returns>
    /// </returns>
    public async Task<Response> InitializeAsync(string adrRootPath = "", string templateRootPath = "", string projectRootPath = "")
    {
        adrRootPath = GetPathWithDefault(adrRootPath, settings.DocFolder ?? settings.DefaultDocFolder);
        projectRootPath = GetPathWithDefault(adrRootPath, settings.DocFolder ?? settings.DefaultTasksFolder);
        templateRootPath = GetPathWithDefault(templateRootPath, settings.TemplateFolder ?? settings.DefaultTemplates);

        settings.DocFolder = adrRootPath;
        settings.TasksFolder = projectRootPath;
        settings.TemplateFolder = templateRootPath;
        settings.Write();

        if (settings.RepositoryInitialized())
        {
            return Response.Ok($"Initialization is already done for {adrRootPath}.");
        }

        var aiConfigured = !string.IsNullOrWhiteSpace(settings.AiSettings.Provider);
        var record = new AdrRecord
        {
            TemplateType = TemplateType.Init,
            Title = "We need ADR's to document architectural decisions",
            Status = AdrStatus.Accepted,
            Context = InitialContext,
            Decision = aiConfigured ? InitialDecision + AiDraftingDecisionNote : InitialDecision,
            Consequences = InitialConsequences
        };
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"Initialization complete, initial ADR is created in {settings.DocFolderInfo().FullName}.");
    }

    private static string GetPathWithDefault(string? folder, string defaultPath)
    {
        folder = folder?.Replace("/", "\\");
        defaultPath = defaultPath.Replace("/", "\\");
        var path = string.IsNullOrEmpty(folder)
            ? defaultPath
            : folder;
        return path.StartsWith("\\")
            ? path[1..]
            : path;
    }

    public async Task<Response> SyncMetadataAsync(int startFromRecordId, int onlyForRecordId)
    {
        var docFolder = settings.DocFolderInfo();
        var templateFolder = settings.TemplateFolderInfo();
        logger.LogInformation($"Documents in {docFolder.FullName}");
        logger.LogInformation($"Templates in {templateFolder.FullName}");

        // check for invalid values
        if (startFromRecordId <= 0)
        {
            return Response.Fail("Invalid start record provided, use positive integer numbers to indicate staring record.");
        }
        if (onlyForRecordId < 0)
        {
            return Response.Fail("Invalid record id provided, use positive integer numbers to identify record for synchronization.");
        }

        return (onlyForRecordId > 0)
         ? await SynchronizeRecord(onlyForRecordId, docFolder)
         : await SynchronizeRange(startFromRecordId, docFolder);
    }

    private async Task<Response> SynchronizeRecord(int onlyForRecordId, IDirectoryInfo docFolder)
    {
        var docInfo = docFolder.EnumerateFiles($"{onlyForRecordId:D5}-*.md").FirstOrDefault();
        if (docInfo == null)
        {
            return Response.Fail($"Could not find ADR with identification {onlyForRecordId}");
        }
        var record = await adrRecordRepository.ReadMetadataAsync(onlyForRecordId);
        var markdown = await adrRecordRepository.ReadContentAsync(onlyForRecordId);
        if (record == null || markdown == null)
        {
            return Response.Fail($"Could not open record or markdown for ADR {onlyForRecordId}");
        }
        return await UpdateFromMarkdown(docInfo, onlyForRecordId, record, markdown);
    }

    private async Task<Response> SynchronizeRange(int startFromRecordId, IDirectoryInfo docFolder)
    {
        var sb = new StringBuilder();
        foreach (var docInfo in docFolder.EnumerateFiles("*.md"))
        {
            var recordIdPart = docInfo.Name.Split('-')[0];
            if (int.TryParse(recordIdPart, out var recordId) && recordId >= startFromRecordId)
            {
                var record = await adrRecordRepository.ReadMetadataAsync(recordId);
                if (record == null)
                {
                    continue;
                }

                var markdown = await adrRecordRepository.ReadContentAsync(recordId);
                if (markdown == null)
                {
                    continue;
                }

                var result = await UpdateFromMarkdown(docInfo, recordId, record, markdown);
                sb.AppendLine($"File: {docInfo.FullName}, Synchronized:{result.Success} {result.Message}");
            }
        }
        return Response.Ok(sb.ToString());
    }

    private async Task<Response> UpdateFromMarkdown(IFileInfo docInfo, int recordId, AdrRecord record, string[] markdown)
    {
        record.UpdateFromMarkdown(recordId, markdown, out var modified);
        if (modified)
        {
            var bytesWritten = await adrRecordRepository.UpdateMetadataAsync(recordId, record);
            if (bytesWritten <= 0)
            {
                return Response.Fail($"Could not find {docInfo.Name} for update");
            }
            else
            {
                return Response.Ok($"Metadatafile {docInfo.Name} is modified.");
            }
        }
        else
        {
            return Response.Ok($"No changes in {docInfo.Name}.");
        }
    }

    public async Task<Response> GenerateTocAsync()
    {
        var toc = new StringBuilder();
        var projectName = settings.ProjectName;

        // Add file description
        toc.AppendLine($"# {projectName}");
        toc.AppendLine();
        toc.AppendLine($"__Date__ : {DateTime.Now:F}");
        toc.AppendLine();
        toc.AppendLine("This file contains the table of contents for the architecture decision records.");
        toc.AppendLine("It is auto generated by the adr-cli tool, any manual modifications are overwritten.");
        toc.AppendLine();

        // Add table header
        toc.AppendLine("# Table of contents");
        toc.AppendLine();
        toc.AppendLine("| Adr | Title | Status |");
        toc.AppendLine("| --- | ----- | ------ |");

        // Add table content
        var docFolder = settings.DocFolderInfo();
        foreach (var docInfo in docFolder.EnumerateFiles("*.md"))
        {
            var recordIdPart = docInfo.Name.Split('-')[0];
            if (int.TryParse(recordIdPart, out var recordId))
            {
                var record = await adrRecordRepository.ReadMetadataAsync(recordId);
                if (record == null)
                {
                    continue;
                }

                // adr-toc.md is written next to docFolder's parent (see CreateRootDocumentAsync), so
                // the link only needs docFolder's own name, not a path back up to the repo root.
                var link = $"{docFolder.Name}/{record.FileName}.md";
                toc.AppendLine($"| {record.RecordId} | [{record.Title}]({link}) | {record.Status} |");
            }
        }
        toc.AppendLine();

        var (success, generatedFile) = await adrRecordRepository.CreateRootDocumentAsync("adr-toc.md", toc);

        return success
            ? Response.Ok($"Generated TOC in {generatedFile}.")
            : Response.Fail($"Generating TOC in {generatedFile} is not completed.");
    }
}