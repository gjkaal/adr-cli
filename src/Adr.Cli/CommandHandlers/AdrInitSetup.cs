using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;

namespace Adr.Cli.CommandHandlers;

public static class CommandHandlerSetup
{
    /// <summary>
    /// Define the command for initializing a new ADR reposity.
    /// </summary>
    public static Command InitCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("init", "Initialize a new ADR folder");
        var adrRoot = CommandOptions.AdrRoot;
        var templateRoot = CommandOptions.TemplateRoot;

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
    public static Command SyncMetadataCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("sync", "Sync the metadata using the content in the markdown files");
        var record = CommandOptions.Record;
        var startAt = CommandOptions.StartAt;

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
    /// Define the command for generating a TOC (table of contents) file.
    /// </summary>
    public static Command GenerateTocCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("generate-toc", "Generate a table of contents markdown file in the project root folder, next to the config file.");
        cmd.SetHandler(async () =>
        {
            var c = serviceProvider.GetRequiredService<IAdrInit>();
            await c.GenerateTocAsync();
        });
        return cmd;
    }
}