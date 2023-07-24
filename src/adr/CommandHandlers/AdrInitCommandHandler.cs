﻿using adr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace CommandHandlers;

public class AdrInitCommandHandler : IAdrInitCommandHandler
{
    private readonly IAdrSettings settings;
    private readonly IFileSystem fileSystem;
    private readonly ILogger<AdrInitCommandHandler> logger;
    private readonly IAdrRecordRepository adrRecordRepository;

    public AdrInitCommandHandler(
        IAdrSettings settings,
        IFileSystem fileSystem,
        ILogger<AdrInitCommandHandler> logger,
        IAdrRecordRepository adrRecordRepository)
    {
        this.settings = settings;
        this.fileSystem = fileSystem;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
    }

    public static Command CommandHandler(IServiceProvider serviceProvider)
    {
        var initCommand = new Command("init", "Get or set the log path");
        Option<string> adrRoot = new("--adrRoot", "Set the adr root directory");
        Option<string> templateRoot = new("--tmpRoot", "Set the template root directory");
        initCommand.AddOption(adrRoot);
        initCommand.AddOption(templateRoot);
        initCommand.SetHandler(async (adrRootPath, templateRootPath) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrInitCommandHandler>();
            await c.InitializeAsync(adrRootPath, templateRootPath);
        }, adrRoot, templateRoot);
        return initCommand;
    }

    public async Task<int> InitializeAsync(string adrRootPath, string templateRootPath)
    {
        adrRootPath = GetWithDefault(adrRootPath, settings.DocFolder??"/adr/doc");
        templateRootPath = GetWithDefault(templateRootPath, settings.TemplateFolder ?? "/adr/template");

        logger.LogInformation($"ADR documents => {adrRootPath}");
        logger.LogInformation($"Templates => {templateRootPath}");

        settings.DocFolder = adrRootPath;
        settings.TemplateFolder = templateRootPath;
        settings.Write();

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
        record.Launch(settings);
        return 0;
    }

    private string GetWithDefault(string? folder, string defaultPath)
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
}
