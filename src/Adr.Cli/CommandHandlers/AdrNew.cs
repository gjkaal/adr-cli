using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Adr.Cli.Ai;
using Adr.Cli.Extensions;
using Adr.Cli.Services;

using McpCore;

using Microsoft.Extensions.Logging;

namespace Adr.Cli.CommandHandlers;

public class AdrNew : IAdrNew
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrNew> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IStdOut stdOut;
    private readonly IProcessHelper processHelper;
    private readonly IAdrLink linkCommandHandler;
    private readonly IAdrProposalGenerator proposalGenerator;
    private readonly IAdrInit adrInit;

    public AdrNew(
        IAdrSettings settings,
        ILogger<AdrNew> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut,
        IProcessHelper processHelper,
        IAdrLink linkCommandHandler,
        IAdrProposalGenerator proposalGenerator,
        IAdrInit adrInit)
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
        this.processHelper = processHelper;
        this.linkCommandHandler = linkCommandHandler;
        this.proposalGenerator = proposalGenerator;
        this.adrInit = adrInit;
    }

    /// <summary>
    /// Create a new ADR
    /// </summary>
    public async Task<Response> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context, bool? useAi)
    {
        if (!settings.RepositoryInitialized())
        {
            return Response.Fail($"Architecture Decision folder is not initialized {settings.DocFolderInfo().FullName}.");
        }

        title = title.NormalizeDashes();
        context = context.NormalizeDashes();

        var effectiveUseAi = useAi ?? !string.IsNullOrWhiteSpace(settings.AiSettings.Provider);

        Response result;
        if (isRequirement)
        {
            logger.LogInformation("Creating Critical Requirement Record.");
            result = await CreateRequirementAsync(title, context, effectiveUseAi);
        }
        else if (!string.IsNullOrEmpty(revisionForRecord) && revisionForRecord != "0")
        {
            logger.LogInformation($"Creating Revision for {revisionForRecord}.");
            if (int.TryParse(revisionForRecord, out var recordId) && recordId > 0)
            {
                result = await CreateRevisionAsync(title, context, recordId, effectiveUseAi);
            }
            else
            {
                logger.LogCritical($"Invalid record id [{revisionForRecord}], it should be a positive integer number.");
                return Response.Fail($"Invalid record id [{revisionForRecord}], it should be a positive integer number.");
            }
        }
        else
        {
            logger.LogInformation($"Creating new decision record.");
            result = await CreateDecisionAsync(title, context, effectiveUseAi);
        }
        return result;
    }

    private async Task<Response> CreateDecisionAsync(string title, string context, bool useAi)
    {
        var record = new AdrRecord
        {
            TemplateType = TemplateType.Ad,
            Title = title,
            Status = AdrStatus.New
        };
        if (!string.IsNullOrEmpty(context))
        {
            record.Context = context;
        }

        var aiWarning = await ApplyAiProposalAsync(record, useAi);
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"AD is created in {settings.DocFolder}.{BuildFallbackNote(record, aiWarning)}");
    }

    private async Task<Response> CreateRevisionAsync(string title, string context, int recordId, bool useAi)
    {
        var superSedes = await adrRecordRepository.ReadMetadataAsync(recordId);
        if (superSedes == null)
        {
            logger.LogCritical("Cannot find a record for revision with id: {RecordId}", recordId);
            return Response.Fail($"Cannot find a record for revision with id: {recordId}");
        }

        var record = new AdrRecord
        {
            TemplateType = TemplateType.Revision,
            SuperSedes = superSedes,
            Title = title,
            Status = AdrStatus.New
        };

        await adrRecordRepository.UpdateMetadataAsync(recordId, record);

        if (!string.IsNullOrEmpty(context))
        {
            record.Context = context;
        }

        var aiWarning = await ApplyAiProposalAsync(record, useAi);
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"Revision for {recordId:D5} is created in {settings.DocFolder}.{BuildFallbackNote(record, aiWarning)}");
    }

    private async Task<Response> CreateRequirementAsync(string title, string context, bool useAi)
    {
        var record = new AdrRecord
        {
            TemplateType = TemplateType.Asr,
            Title = title,
            Status = AdrStatus.New
        };
        if (!string.IsNullOrEmpty(context))
        {
            record.Context = context;
        }

        var aiWarning = await ApplyAiProposalAsync(record, useAi);
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"ASR is created in {settings.DocFolder}.{BuildFallbackNote(record, aiWarning)}");
    }

    /// <summary>
    /// Replace Decision and/or Consequences in place on an existing ADR's markdown - a convenience
    /// alternative to hand-editing the .md file directly. Both fields are markdown-only (never
    /// stored in the .json metadata - see AdrRecord's [JsonIgnore] on Decision/Consequences), so
    /// this never touches metadata and never requires a follow-up adr_sync.
    /// </summary>
    public async Task<Response> UpdateContentAsync(int recordId, string? decision, string? consequences)
    {
        if (string.IsNullOrWhiteSpace(decision) && string.IsNullOrWhiteSpace(consequences))
        {
            return Response.Fail("Provide at least one of decision or consequences to update.");
        }

        var record = await adrRecordRepository.ReadMetadataAsync(recordId);
        if (record == null)
        {
            return Response.Fail($"Cannot find a record with id: {recordId}");
        }

        var content = await adrRecordRepository.ReadContentAsync(recordId);
        if (content.Length == 0)
        {
            return Response.Fail($"Cannot find markdown content for ADR {recordId}");
        }

        var updatedFields = new List<string>();
        if (!string.IsNullOrWhiteSpace(decision))
        {
            content = content.ReplaceMdContent("Decision", SplitIntoLines(decision.NormalizeDashes())).ToArray();
            updatedFields.Add("Decision");
        }
        if (!string.IsNullOrWhiteSpace(consequences))
        {
            content = content.ReplaceMdContent("Consequences", SplitIntoLines(consequences.NormalizeDashes())).ToArray();
            updatedFields.Add("Consequences");
        }

        var bytesWritten = await adrRecordRepository.UpdateContentAsync(record, content);
        return bytesWritten > 0
            ? Response.Ok($"Updated {string.Join(" and ", updatedFields)} for ADR {recordId:D5}.")
            : Response.Fail($"Could not write updated content for ADR {recordId:D5}.");
    }

    /// <summary>
    /// Change an ADR's status - see IAdrNew.UpdateStatusAsync. Writes markdown and metadata in the
    /// same call (the defect this closes: adr_new's Status was already written to both, but the two
    /// files could still only be brought back into agreement via a hand-edit + adr_sync round trip),
    /// then regenerates adr-toc.md since every status change invalidates it.
    /// </summary>
    public async Task<Response> UpdateStatusAsync(int recordId, AdrStatus status, string justification)
    {
        var record = await adrRecordRepository.ReadMetadataAsync(recordId);
        if (record == null)
        {
            return Response.Fail($"Cannot find a record with id: {recordId}");
        }

        var content = await adrRecordRepository.ReadContentAsync(recordId);
        if (content.Length == 0)
        {
            return Response.Fail($"Cannot find markdown content for ADR {recordId}");
        }

        record.Status = status;
        record.Logs.Add(new AdrStatusUpdate { DateTime = DateTime.UtcNow, Status = status, Justification = justification });

        var metaWritten = await adrRecordRepository.UpdateMetadataAsync(recordId, record);
        if (metaWritten <= 0)
        {
            return Response.Fail($"Could not update metadata for ADR {recordId:D5}.");
        }

        content = content.ReplaceMdContent("Status", [$"__{status}__"]).ToArray();
        var contentWritten = await adrRecordRepository.UpdateContentAsync(record, content);
        if (contentWritten <= 0)
        {
            return Response.Fail($"Could not update markdown for ADR {recordId:D5}.");
        }

        var tocResult = await adrInit.GenerateTocAsync();
        var tocNote = tocResult.Success ? string.Empty : $" (adr-toc.md was not regenerated: {tocResult.Message})";

        return Response.Ok($"ADR {recordId:D5} status set to {status}.{tocNote}");
    }

    private static string[] SplitIntoLines(string text)
    {
        return text.ReplaceLineEndings("\n").Split('\n');
    }

    /// <summary>
    /// Appended to the Response on a successful AI draft, so an agent driving this tool over MCP is
    /// told to check the draft rather than treat it as final - the model can still produce plausible
    /// but wrong content, and this is the one place in the pipeline where a human hasn't looked yet.
    /// </summary>
    private const string VerifyAiDraftNote =
        " AI drafted the Context/Decision/Consequences for this ADR - verify the resulting file before " +
        "treating it as final, and if anything in it is unclear, use the grill-me skill with the user " +
        "rather than guessing.";

    /// <summary>
    /// Appended to the Response whenever Decision/Consequences were left blank and will render as
    /// template placeholder text - covers the case where AI wasn't attempted at all (no <c>aiWarning</c>
    /// from <see cref="ApplyAiProposalAsync" />), which otherwise produced no signal that the ADR
    /// still needs manual content.
    /// </summary>
    private const string PlaceholderFallbackNote =
        " Decision and/or Consequences were left blank and will be written as placeholder text - " +
        "fill them in before treating this ADR as final.";

    /// <summary>
    /// Picks the note to append to the command's Response: the AI outcome message if AI was
    /// attempted, otherwise a placeholder warning if Decision/Consequences are still empty at this
    /// point (they will render as template defaults), otherwise nothing.
    /// </summary>
    private static string BuildFallbackNote(AdrRecord record, string aiWarning)
    {
        if (!string.IsNullOrEmpty(aiWarning))
        {
            return aiWarning;
        }

        return string.IsNullOrEmpty(record.Decision) || string.IsNullOrEmpty(record.Consequences)
            ? PlaceholderFallbackNote
            : string.Empty;
    }

    /// <summary>
    /// Draft Context/Decision/Consequences for <paramref name="record" /> using the configured AI
    /// provider. A no-op when <paramref name="useAi" /> is false. On any AI failure, logs a warning
    /// and leaves the record exactly as it was - the ADR is still created from the template, with
    /// Decision/Consequences falling back to placeholder text. A user-supplied Context is
    /// preserved rather than overwritten by the AI's draft.
    /// </summary>
    /// <returns>
    /// Empty string when AI wasn't requested. Otherwise a message describing the outcome - success or
    /// failure - meant to be appended to the command's Response so it reaches the caller even when
    /// logging is unavailable (Release builds) or stdout is muted (MCP mode).
    /// </returns>
    private async Task<string> ApplyAiProposalAsync(AdrRecord record, bool useAi)
    {
        if (!useAi)
        {
            return string.Empty;
        }

        var hadUserSuppliedContext = !string.IsNullOrEmpty(record.Context);
        var existingRecords = await GetExistingAdrSummariesAsync();
        var result = await proposalGenerator.GenerateAsync(record.Title, record.Context, existingRecords, record.TemplateType.ToString());
        if (!result.Success || result.Value == null)
        {
            logger.LogWarning("AI proposal generation failed, continuing without it: {Message}", result.Message);
            stdOut.WriteLine($"AI proposal generation failed, continuing without it: {result.Message}");
            return $" AI proposal generation failed, Decision/Consequences left as template defaults: {result.Message}";
        }

        if (!hadUserSuppliedContext && !string.IsNullOrEmpty(result.Value.Context))
        {
            record.Context = result.Value.Context.NormalizeDashes();
        }
        record.Decision = result.Value.Decision.NormalizeDashes();
        record.Consequences = result.Value.Consequences.NormalizeDashes();
        return VerifyAiDraftNote;
    }

    private async Task<IReadOnlyList<AdrSummary>> GetExistingAdrSummariesAsync()
    {
        var summaries = new List<AdrSummary>();
        foreach (var file in settings.DocFolderInfo().EnumerateFiles("*.md"))
        {
            var separatorIndex = file.Name.IndexOf('-');
            if (separatorIndex <= 0 || !int.TryParse(file.Name[..separatorIndex], out var recordId))
            {
                continue;
            }

            var record = await adrRecordRepository.ReadMetadataAsync(recordId);
            if (record == null)
            {
                continue;
            }

            summaries.Add(new AdrSummary
            {
                RecordId = record.RecordId,
                Title = record.Title,
                Status = record.Status,
                Context = record.Context
            });
        }

        return summaries;
    }

    public async Task<Response> CopyAdrAsync(string sourceId, bool isRevision)
    {
        if (!int.TryParse(sourceId, out var recordId))
        {
            stdOut.WriteLine($"Expecting a numeric value for source and it was {sourceId}.");
            return Response.Fail($"Expecting a numeric value for source and it was {sourceId}.");
        }

        var record = await adrRecordRepository.ReadMetadataAsync(recordId);
        if (record == null)
        {
            logger.LogCritical("Cannot find a record for with id: {RecordId}", recordId);
            return Response.Fail($"Cannot find a record for with id: {recordId}");
        }

        var newId = settings.GetNextFileNumber(settings.DocFolderInfo());
        var newRecord = await adrRecordRepository.CopyRecordAsync(record, newId, isRevision);

        Response linkResult;
        if (isRevision)
        {
            linkResult = await linkCommandHandler.HandleLinkAdrAsync(newId, recordId, "Supersedes", AdrLinkTypeOperation.Create);
        }
        else
        {
            linkResult = await linkCommandHandler.HandleLinkAdrAsync(newId, recordId, "Copied from", AdrLinkTypeOperation.Create);
        }

        if (!linkResult.Success)
        {
            logger.LogWarning("Could not link records {RecordId} and {NewId}.", recordId, newId);
        }

        newRecord.LaunchEditor(settings, processHelper);

        return Response.Ok($"Copy for {recordId:D5} is created as {newId:D5} in {settings.DocFolder}.");
    }
}