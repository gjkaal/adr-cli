using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrNewSetup
{
    public static Command NewAdrCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>(); var cmd = new Command("new", "Create a new Architecture Decision Record");
        var title = CommandOptions.Title;
        var requirement = CommandOptions.Requirement;
        var revision = CommandOptions.Revision;
        var context = CommandOptions.Context;

        title.Required = true;

        cmd.Options.Add(title);
        cmd.Options.Add(requirement);
        cmd.Options.Add(revision);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var titleValue = ctx.GetValue(title) ?? "";
            var revisionValue = ctx.GetValue(revision) ?? "";
            var requirementValue = ctx.GetValue(requirement);
            var contextValue = ctx.GetValue(context) ?? "";

            var c = serviceProvider.GetRequiredService<IAdrNew>();
            var result = await c.NewAdrAsync(titleValue, requirementValue, revisionValue, contextValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command CopyAdrCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>(); var cmd = new Command("copy", "Copy an existing ADR to a new ADR");
        var sourceId = CommandOptions.SourceId;
        var revision = CommandOptions.AsRevision;

        sourceId.Required = true;

        cmd.Options.Add(sourceId);
        cmd.Options.Add(revision);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var revisionValue = ctx.GetValue(revision);

            var c = serviceProvider.GetRequiredService<IAdrNew>();
            var result = await c.CopyAdrAsync(sourceIdValue, revisionValue);
            stdOut.Write(result);
        });
        return cmd;
    }
}