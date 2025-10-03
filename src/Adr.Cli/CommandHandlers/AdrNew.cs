using System.Threading.Tasks;

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

    public AdrNew(
        IAdrSettings settings,
        ILogger<AdrNew> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut,
        IProcessHelper processHelper,
        IAdrLink linkCommandHandler)
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
        this.processHelper = processHelper;
        this.linkCommandHandler = linkCommandHandler;
    }

    /// <summary>
    /// Create a new ADR
    /// </summary>
    public async Task<Response> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context)
    {
        if (!settings.RepositoryInitialized())
        {
            stdOut.WriteLine($"Architecture Decision folder is not initialized {settings.DocFolderInfo().FullName}.");
            return Response.Fail($"Architecture Decision folder is not initialized {settings.DocFolderInfo().FullName}.");
        }

        Response result;
        if (isRequirement)
        {
            logger.LogInformation("Creating Critical Requirement Record.");
            result = await CreateRequirementAsync(title, context);
        }
        else if (!string.IsNullOrEmpty(revisionForRecord) && revisionForRecord != "0")
        {
            logger.LogInformation($"Creating Revision for {revisionForRecord}.");
            if (int.TryParse(revisionForRecord, out var recordId) && recordId > 0)
            {
                result = await CreateRevisionAsync(title, context, recordId);
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
            result = await CreateDecisionAsync(title, context);
        }
        return result;
    }

    private async Task<Response> CreateDecisionAsync(string title, string context)
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

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"AD is created in {settings.DocFolder}.");
    }

    private async Task<Response> CreateRevisionAsync(string title, string context, int recordId)
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

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"Revision for {recordId:D5} is created in {settings.DocFolder}.");
    }

    private async Task<Response> CreateRequirementAsync(string title, string context)
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

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        return Response.Ok($"ASR is created in {settings.DocFolder}.");
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

        var newId = settings.GetNextFileNumber();
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