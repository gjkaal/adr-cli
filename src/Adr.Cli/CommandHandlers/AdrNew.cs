using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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
    public async Task<int> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context)
    {
        if (!settings.RepositoryInitialized())
        {
            stdOut.WriteLine($"Architecture Decision folder is not initialized {settings.DocFolderInfo().FullName}.");
            return -1;
        }

        int result;
        if (isRequirement)
        {
            logger.LogInformation("Creating Critical Requirement Record.");
            result = await CreateRequirementAsync(title, context);
        }
        else if (!string.IsNullOrEmpty(revisionForRecord))
        {
            logger.LogInformation($"Creating Revision for {revisionForRecord}.");
            if (int.TryParse(revisionForRecord, out var recordId))
            {
                result = await CreateRevisionAsync(title, context, recordId);
            }
            else
            {
                logger.LogCritical($"Invalid record id [{revisionForRecord}], it should be a positive integer number.");
                result = -1;
            }
        }
        else
        {
            logger.LogInformation($"Creating new decision record.");
            result = await CreateDecisionAsync(title, context);
        }
        return result;
    }

    private async Task<int> CreateDecisionAsync(string title, string context)
    {
        var record = new AdrRecord
        {
            TemplateType = TemplateType.Ad,
            Title = title,
            Status = AdrStatus.New
        };
        if (!string.IsNullOrEmpty(context)) record.Context = context;

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        stdOut.WriteLine($"AD is created in {settings.DocFolder}.");
        return 0;
    }

    private async Task<int> CreateRevisionAsync(string title, string context, int recordId)
    {
        var superSedes = await adrRecordRepository.ReadMetadataAsync(recordId);
        if (superSedes == null)
        {
            logger.LogCritical($"Cannot find a record for revision with id: {recordId}");
            return -1;
        }

        var record = new AdrRecord
        {
            TemplateType = TemplateType.Revision,
            SuperSedes = superSedes,
            Title = title,
            Status = AdrStatus.New
        };

        await adrRecordRepository.UpdateMetadataAsync(recordId, record);

        if (!string.IsNullOrEmpty(context)) record.Context = context;

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        stdOut.WriteLine($"Revision for {recordId:D5} is created in {settings.DocFolder}.");
        return 0;
    }

    private async Task<int> CreateRequirementAsync(string title, string context)
    {
        var record = new AdrRecord
        {
            TemplateType = TemplateType.Asr,
            Title = title,
            Status = AdrStatus.New
        };
        if (!string.IsNullOrEmpty(context)) record.Context = context;

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        stdOut.WriteLine($"ASR is created in {settings.DocFolder}.");
        return 0;
    }

    public async Task<int> CopyAdrAsync(string sourceId, bool isRevision)
    {
        if (!int.TryParse(sourceId, out var recordId)) {
            stdOut.WriteLine($"Expecting a numeric value for source and it was {sourceId}.");
            return 0; 
        }

        var record = await adrRecordRepository.ReadMetadataAsync(recordId);
        if (record == null)
        {
            logger.LogCritical($"Cannot find a record for with id: {recordId}");
            return -1;
        }

        var newId = settings.GetNextFileNumber();
        var newRecord = await adrRecordRepository.CopyRecordAsync(record, newId, isRevision);

        int linkResult;
        if (isRevision)
        {
            linkResult = await linkCommandHandler.HandleLinkAdrAsync(newId, recordId, "Supersedes", AdrLinkTypeOperation.Create);
        }
        else
        {
            linkResult = await linkCommandHandler.HandleLinkAdrAsync(newId, recordId, "Copied from", AdrLinkTypeOperation.Create);
        }

        if (linkResult != 0)
        {
            logger.LogWarning($"Could not link records {recordId} and {newId}.");
        }

        newRecord.LaunchEditor(settings, processHelper);

        stdOut.WriteLine($"Copy for {recordId:D5} is created as {newId:D5} in {settings.DocFolder}.");
        return 0;
    }
}