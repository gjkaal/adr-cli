using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class CommandHandlerSetup
{
    /// <summary>
    /// Define the command for initializing a new ADR reposity.
    /// </summary>
    public static Command InitCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("init", "Initialize a new ADR folder");
        var adrRoot = CommandOptions.AdrRoot;
        var templateRoot = CommandOptions.TemplateRoot;
        var projectRoot = CommandOptions.ProjectRoot;

        cmd.Options.Add(adrRoot);
        cmd.Options.Add(templateRoot);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var adrRootPath = ctx.GetValue(adrRoot) ?? "";
            var templateRootPath = ctx.GetValue(templateRoot) ?? "";
            var projectRootPath = ctx.GetValue(projectRoot) ?? "";

            var c = serviceProvider.GetRequiredService<IAdrInit>();
            var result = await c.InitializeAsync(adrRootPath, templateRootPath, projectRootPath);
            stdOut.Write(result);
        });

        return cmd;
    }

    /// <summary>
    /// Define the command for synchronizing the metadata documents in the ADR reposity.
    /// </summary>
    public static Command SyncMetadataCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("sync", "Sync the metadata using the content in the markdown files");
        var record = CommandOptions.Record;
        var startAt = CommandOptions.StartAt;

        cmd.Options.Add(record);
        cmd.Options.Add(startAt);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var startAtValue = ctx.GetValue(startAt) ?? "-1";
            var recordValue = ctx.GetValue(record) ?? "-1";

            var startAtid = 1;
            var recordId = 0;
            if (!string.IsNullOrEmpty(startAtValue) && !int.TryParse(startAtValue, out startAtid))
            {
                startAtid = -1;
            }

            if (!string.IsNullOrEmpty(recordValue) && !int.TryParse(recordValue, out recordId))
            {
                recordId = -1;
            }

            var c = serviceProvider.GetRequiredService<IAdrInit>();
            var result = await c.SyncMetadataAsync(startAtid, recordId);
            stdOut.Write(result);
        });
        return cmd;
    }

    /// <summary>
    /// Define the command for generating a TOC (table of contents) file.
    /// </summary>
    public static Command GenerateTocCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("generate-toc", "Generate a table of contents markdown file in the project root folder, next to the config file.");
        cmd.SetAction(async (ParseResult ctx) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrInit>();
            var result = await c.GenerateTocAsync();
            stdOut.Write(result);
        });
        return cmd;
    }
}