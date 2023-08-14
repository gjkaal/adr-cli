using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;

namespace Adr.Cli.CommandHandlers;

public static class AdrNewSetup
{
    public static Command NewAdrCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("new", "Create a new Architecture Decision Record");
        var title = CommandOptions.Title;
        var requirement = CommandOptions.Requirement;
        var revision = CommandOptions.Revision;
        var context = CommandOptions.Context;

        title.IsRequired = true;

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

    public static Command CopyAdrCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("copy", "Copy an existing ADR to a new ADR");
        var sourceId = CommandOptions.SourceId;
        var revision = CommandOptions.AsRevision;

        sourceId.IsRequired = true;

        cmd.AddOption(sourceId);
        cmd.AddOption(revision);

        cmd.SetHandler(async (sourceId, revision) =>
        { 
            var c = serviceProvider.GetRequiredService<IAdrNew>();
            await c.CopyAdrAsync(sourceId, revision);
        }, sourceId, revision);
        return cmd;
    }
}