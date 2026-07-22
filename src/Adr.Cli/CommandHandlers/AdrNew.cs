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
    public async Task<Response> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context, bool useAi)
    {
        if (!settings.RepositoryInitialized())
        {
            return Response.Fail($"Architecture Decision folder is not initialized {settings.DocFolderInfo().FullName}.");
        }

        Response result;
        if (isRequirement)
        {
            logger.LogInformation("Creating Critical Requirement Record.");
            result = await CreateRequirementAsync(title, context, useAi);
        }
        else if (!string.IsNullOrEmpty(revisionForRecord) && revisionForRecord != "0")
        {
            logger.LogInformation($"Creating Revision for {revisionForRecord}.");
            if (int.TryParse(revisionForRecord, out var recordId) && recordId > 0)
            {
                result = await CreateRevisionAsync(title, context, recordId, useAi);
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
            result = await CreateDecisionAsync(title, context, useAi);
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

        await ApplyAiProposalAsync(record, useAi);
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"AD is created in {settings.DocFolder}.");
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

        await ApplyAiProposalAsync(record, useAi);
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"Revision for {recordId:D5} is created in {settings.DocFolder}.");
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

        await ApplyAiProposalAsync(record, useAi);
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"ASR is created in {settings.DocFolder}.");
    }

    /// <summary>
    /// Draft Decision/Consequences for <paramref name="record" /> using the configured AI provider.
    /// A no-op when <paramref name="useAi" /> is false. On any AI failure, logs a warning and leaves
    /// the record exactly as it was - the ADR is still created from the template.
    /// </summary>
    private async Task ApplyAiProposalAsync(AdrRecord record, bool useAi)
    {
        if (!useAi)
        {
            return;
        }

        var existingRecords = await GetExistingAdrSummariesAsync();
        var result = await proposalGenerator.GenerateAsync(record.Title, record.Context, existingRecords);
        if (!result.Success || result.Value == null)
        {
            logger.LogWarning("AI proposal generation failed, continuing without it: {Message}", result.Message);
            stdOut.WriteLine($"AI proposal generation failed, continuing without it: {result.Message}");
            return;
        }

        record.Decision = result.Value.Decision;
        record.Consequences = result.Value.Consequences;
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