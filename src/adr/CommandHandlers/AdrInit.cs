using adr.Extensions;
using adr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace adr.CommandHandlers;

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
        var initCommand = new Command("init", "Initialize a new ADR folder");
        Option<string> adrRoot = new("--adrRoot", "Set the adr root directory");
        Option<string> templateRoot = new("--tmpRoot", "Set the template root directory");
        initCommand.AddOption(adrRoot);
        initCommand.AddOption(templateRoot);
        initCommand.SetHandler(async (adrRootPath, templateRootPath) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrInit>();
            await c.InitializeAsync(adrRootPath, templateRootPath);
        }, adrRoot, templateRoot);
        return initCommand;
    }

    /// <summary>
    /// Define the command for synchronizing the metadata documents in the ADR reposity.
    /// </summary>
    private static Command SyncMetadataCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("sync", "Sync the metadata using the content in the markdown files");
        cmd.SetHandler(async () =>
        {
            var c = serviceProvider.GetRequiredService<IAdrInit>();
            await c.SyncMetadataAsync(1);
        });
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
        var saveSettings = false;
        if (!string.IsNullOrEmpty(adrRootPath) || !string.IsNullOrEmpty(adrRootPath))
        {
            saveSettings = true;
        }

        adrRootPath = GetPathWithDefault(adrRootPath, settings.DocFolder ?? settings.DefaultDocFolder);
        templateRootPath = GetPathWithDefault(templateRootPath, settings.TemplateFolder ?? settings.DefaultTemplates);

        settings.DocFolder = adrRootPath;
        settings.TemplateFolder = templateRootPath;
        if (saveSettings)
        {
            settings.Write();
        }

        if (settings.RepositoryInitialized())
        {
            logger.LogError("Initialization failed, folder contains files.");
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

        logger.LogInformation($"Initialization complete, initial ADR is created in {settings.DocFolder}.");
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

    public  async Task<int> SyncMetadataAsync(int startFromRecordId) {
        var docFolder = settings.DocFolderInfo();
        foreach(var docInfo in docFolder.EnumerateFiles("*.md"))
        {
            var recordIdPart = docInfo.Name.Split('-')[0];
            if (int.TryParse(recordIdPart, out var recordId) && recordId >= startFromRecordId) {
                var record = await adrRecordRepository.ReadMetadataAsync(recordId);
                if (record == null) continue;
                var markdown = await adrRecordRepository.ReadContentAsync(recordId);
                if (markdown == null) continue;
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
        return 0;
    }
}

