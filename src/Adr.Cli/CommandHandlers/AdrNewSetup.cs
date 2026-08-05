using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrNewSetup
{
    public static Command NewAdrCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>(); var cmd = new Command("new", "Create a new Architecture Decision Record: a decision and its consequences, not a unit of work to be done (see 'task-new' for that).");
        var title = CommandOptions.Title;
        var requirement = CommandOptions.Requirement;
        var revision = CommandOptions.Revision;
        var context = CommandOptions.Context;
        var useAi = CommandOptions.UseAi;

        title.Required = true;

        cmd.Options.Add(title);
        cmd.Options.Add(requirement);
        cmd.Options.Add(revision);
        cmd.Options.Add(context);
        cmd.Options.Add(useAi);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var titleValue = ctx.GetValue(title) ?? "";
            var revisionValue = ctx.GetValue(revision) ?? "";
            var requirementValue = ctx.GetValue(requirement);
            var contextValue = ctx.GetValue(context) ?? "";
            var useAiValue = ctx.GetValue(useAi);

            var c = serviceProvider.GetRequiredService<IAdrNew>();
            var result = await c.NewAdrAsync(titleValue, requirementValue, revisionValue, contextValue, useAiValue);
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

    public static Command UpdateContentCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("update-content", "Replace the Decision and/or Consequences section of an existing ADR's markdown, in place - a convenience alternative to hand-editing the .md file directly.");
        var record = CommandOptions.Record;
        var decision = CommandOptions.Decision;
        var consequences = CommandOptions.Consequences;

        record.Required = true;

        cmd.Options.Add(record);
        cmd.Options.Add(decision);
        cmd.Options.Add(consequences);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var recordValue = ctx.GetValue(record) ?? "";
            var decisionValue = ctx.GetValue(decision);
            var consequencesValue = ctx.GetValue(consequences);

            if (!int.TryParse(recordValue, out var recordId) || recordId <= 0)
            {
                stdOut.Write(McpCore.Response.Fail($"Invalid record id [{recordValue}], it should be a positive integer number."));
                return;
            }

            var c = serviceProvider.GetRequiredService<IAdrNew>();
            var result = await c.UpdateContentAsync(recordId, decisionValue, consequencesValue);
            stdOut.Write(result);
        });
        return cmd;
    }
}