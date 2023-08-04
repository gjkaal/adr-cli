using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO.Abstractions;
using System.Linq;
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

    public static IEnumerable<Command> CommandHandler(IServiceProvider serviceProvider)
    {
        return new[] {
            InitCommand(serviceProvider),
            SyncMetadataCommand(serviceProvider)
        };
    }

    /// <summary>
    /// Define the command for initializing a new ADR reposity.
    /// </summary>
    private static Command InitCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("init", "Initialize a new ADR folder");
        Option<string> adrRoot = new("--adrRoot", "Set the adr root directory");
        Option<string> templateRoot = new("--tmpRoot", "Set the template root directory");
        cmd.AddOption(adrRoot);
        cmd.AddOption(templateRoot);
        cmd.SetHandler(async (adrRootPath, templateRootPath) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrInit>();
            await c.InitializeAsync(adrRootPath, templateRootPath);
        }, adrRoot, templateRoot);
        return cmd;
    }

    /// <summary>
    /// Define the command for synchronizing the metadata documents in the ADR reposity.
    /// </summary>
    private static Command SyncMetadataCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("sync", "Sync the metadata using the content in the markdown files");
        Option<string> startAt = new("--startAt", "Synchronize from this record until the end");
        Option<string> record = new("--record", "Synchronize only for a single record");
        cmd.AddOption(record);
        cmd.SetHandler(async (startAt, record) =>
        {
            var startAtid = 1;
            var recordId = 0;
            if (!string.IsNullOrEmpty(startAt) && !int.TryParse(startAt, out startAtid)) startAtid = -1;
            if (!string.IsNullOrEmpty(record) && !int.TryParse(record, out recordId)) recordId = -1;
            var c = serviceProvider.GetRequiredService<IAdrInit>();
            await c.SyncMetadataAsync(startAtid, recordId);
        }, startAt, record);
        return cmd;
    }

    /// <summary>
    /// Initialize an ADR, with optionally providing a path where the documents are stored and
    /// a path where the templates can be found. The settings are stored in a config file.
    /// </summary>
    /// <param name="adrRootPath">An alternate for the document folder, default is '\docs\adr'.</param>
    /// <param name="templateRootPath">An alternate for the template folder, default is '\docs\adr\template' </param>
    /// <returns></returns>
    public async Task<int> InitializeAsync(string adrRootPath = "", string templateRootPath = "")
    {
        adrRootPath = GetPathWithDefault(adrRootPath, settings.DocFolder ?? settings.DefaultDocFolder);
        templateRootPath = GetPathWithDefault(templateRootPath, settings.TemplateFolder ?? settings.DefaultTemplates);

        var localFolder = settings.CurrentPath;
        settings.DocFolder = adrRootPath;
        settings.TemplateFolder = templateRootPath;
        settings.Write();

        if (settings.RepositoryInitialized())
        {
            stdOut.WriteLine($"Initialization is already done for {adrRootPath}.");
            return -1;
        }

        var record = new AdrRecord
        {
            TemplateType = TemplateType.Init,
            Title = "Record Architecture Decisions initialization",
            Status = AdrStatus.Accepted
        };
        await adrRecordRepository.WriteRecordAsync(record);
        record.LaunchEditor(settings, processHelper);

        stdOut.WriteLine($"Initialization complete, initial ADR is created in {settings.DocFolderInfo().FullName}.");
        return 0;
    }

    private static string GetPathWithDefault(string? folder, string defaultPath)
    {
        folder = folder?.Replace("/", "\\");
        defaultPath = defaultPath.Replace("/", "\\");
        var path = string.IsNullOrEmpty(folder)
            ? defaultPath
            : folder;
        return (path.StartsWith("\\"))
            ? path[1..]
            : path;
    }

    public  async Task<int> SyncMetadataAsync(int startFromRecordId, int onlyForRecordId)
    {
        var docFolder = settings.DocFolderInfo();
        var templateFolder = settings.TemplateFolderInfo();
        logger.LogInformation($"Documents in {docFolder.FullName}");
        logger.LogInformation($"Templates in {templateFolder.FullName}");

        // check for invalid values
        if (startFromRecordId <= 0)
        {
            stdOut.WriteLine("Invalid start record provided, use positive integer numbers to indicate staring record.");
            return -1;
        }
        if (onlyForRecordId <= 0)
        {
            stdOut.WriteLine("Invalid record id provided, use positive integer numbers to identify record for syncronization.");
            return -1;
        }

        return (onlyForRecordId > 0)
         ? await SynchronizeRecord(onlyForRecordId, docFolder)
         : await SynchronizeRange(startFromRecordId, docFolder);        
    }


    private async Task<int> SynchronizeRecord(int onlyForRecordId, IDirectoryInfo docFolder)
    {
        var docInfo = docFolder.EnumerateFiles($"{onlyForRecordId:D5}-*.md").FirstOrDefault();
        if (docInfo == null)
        {
            stdOut.WriteLine($"Could not find ADR with identification {onlyForRecordId}");
            return -1;
        }
        var record = await adrRecordRepository.ReadMetadataAsync(onlyForRecordId);
        var markdown = await adrRecordRepository.ReadContentAsync(onlyForRecordId);
        if (record == null || markdown == null)
        {
            stdOut.WriteLine($"Could not open record or markdown for ADR {onlyForRecordId}");
            return -1;
        }
        await UpdateFromMarkdown(docInfo, onlyForRecordId, record, markdown);
        return 0;
    }

    private async Task<int> SynchronizeRange(int startFromRecordId, IDirectoryInfo docFolder)
    {
        foreach (var docInfo in docFolder.EnumerateFiles("*.md"))
        {
            var recordIdPart = docInfo.Name.Split('-')[0];
            if (int.TryParse(recordIdPart, out var recordId) && recordId >= startFromRecordId)
            {
                var record = await adrRecordRepository.ReadMetadataAsync(recordId);
                if (record == null) continue;
                var markdown = await adrRecordRepository.ReadContentAsync(recordId);
                if (markdown == null) continue;
                await UpdateFromMarkdown(docInfo, recordId, record, markdown);
            }
        }
        return 0;
    }

    private async Task UpdateFromMarkdown(IFileInfo docInfo, int recordId, AdrRecord record, string[] markdown)
    {
        record.UpdateFromMarkdown(recordId, markdown, out var modified);
        if (modified)
        {
            var bytesWritten = await adrRecordRepository.UpdateMetadataAsync(recordId, record);
            if (bytesWritten <= 0)
            {
                stdOut.WriteLine($"Could not find {docInfo.Name} for update");
            }
            else
            {
                stdOut.WriteLine($"Metadatafile {docInfo.Name} is modified.");
            }
        }
    }
}

