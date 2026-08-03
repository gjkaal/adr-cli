using System.Collections.Generic;
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

    public AdrNew(
        IAdrSettings settings,
        ILogger<AdrNew> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut,
        IProcessHelper processHelper,
        IAdrLink linkCommandHandler,
        IAdrProposalGenerator proposalGenerator)
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
        this.processHelper = processHelper;
        this.linkCommandHandler = linkCommandHandler;
        this.proposalGenerator = proposalGenerator;
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
            record.Context = result.Value.Context;
        }
        record.Decision = result.Value.Decision;
        record.Consequences = result.Value.Consequences;
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