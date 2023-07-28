using adr.Extensions;
using adr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;

namespace adr.CommandHandlers;

public class AdrNew : IAdrNew
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrNew> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IStdOut stdOut;
    private readonly IProcessHelper processHelper;

    public AdrNew(
        IAdrSettings settings,
        ILogger<AdrNew> logger,
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

    public static IEnumerable<Command> CommandHandler(IServiceProvider serviceProvider)
    {
        return new[] { InitNewCommand(serviceProvider) };
    }

    private static readonly Option<string> title = new("--title", "The title for the ADR")
    {
        IsRequired = true
    };

    private static readonly Option<bool> requirement = new("--req", "The ADR is a critical requirement.")
    {
        IsRequired = false
    };

    private static readonly Option<string> revision = new("--rev", "The ADR rivision for an earlier ADR, provide a valid id.")
    {
        IsRequired = false
    };

    private static readonly Option<string> context = new("--context", "Optional context for the ADR (otherwise a default value will be used)")
    {
        IsRequired = false
    };

    private static Command InitNewCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("new", "Create a new Architecture Decision Record");

        cmd.AddOption(title);
        cmd.AddOption(requirement);
        cmd.AddOption(revision);

        cmd.SetHandler(async (title, requirement, revision, context) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrNew>();
            await c.NewAdrAsync(title, requirement, revision, context);
        }, title, requirement, revision, context);
        return cmd;
    }

    /// <summary>
    /// Create a new ADR
    /// </summary>
    public async Task<int> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context) {

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

    private async Task<int> CreateDecisionAsync(string title, string context) {
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

    private async Task<int> CreateRevisionAsync(string title, string context, int recordId) {
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
        if(!string.IsNullOrEmpty(context)) record.Context = context;

        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        stdOut.WriteLine($"Revision for {recordId:D5} is created in {settings.DocFolder}.");
        return 0;
    }

    private async Task<int> CreateRequirementAsync(string title, string context) {
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
}

