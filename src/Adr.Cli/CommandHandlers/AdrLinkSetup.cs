using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrLinkSetup
{
    public static Command LinkCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("link", "Link 2 ADR's for ammend / clarify or some other reason");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;
        var reason = CommandOptions.Reason;

        sourceId.Required = true;
        sourceId.Aliases.Add("-s");

        targetId.Aliases.Add("-t");
        reason.Aliases.Add("-r");

        cmd.Options.Add(sourceId);
        cmd.Options.Add(targetId);
        cmd.Options.Add(reason);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var targetIdValue = ctx.GetValue(targetId) ?? "";
            var reasonValue = ctx.GetValue(reason) ?? "";

            var c = serviceProvider.GetRequiredService<IAdrLink>();
            var result = await c.HandleLinkAdrAsync(sourceIdValue, targetIdValue, reasonValue, AdrLinkTypeOperation.Create);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command UnLinkCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("rlink", "Remove all links from one ADR to another");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;

        sourceId.Aliases.Add("-s");
        targetId.Aliases.Add("-t");

        cmd.Options.Add(sourceId);
        cmd.Options.Add(targetId);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var targetIdValue = ctx.GetValue(targetId) ?? "";
            var c = serviceProvider.GetRequiredService<IAdrLink>();
            var result = await c.HandleLinkAdrAsync(sourceIdValue, targetIdValue, "remove", AdrLinkTypeOperation.Remove);
            stdOut.Write(result);
        });
        return cmd;
    }


    public static Command LinkTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("adr-link-task", "Record that a task belongs to an ADR (see AdrRecord.RelatedTasks / ADR 00010), so adr-export can attach it as a GitHub sub-issue.");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;
        var reason = CommandOptions.Reason;

        sourceId.Required = true;
        sourceId.Aliases.Add("-s");
        targetId.Required = true;
        targetId.Aliases.Add("-t");
        reason.Aliases.Add("-r");

        cmd.Options.Add(sourceId);
        cmd.Options.Add(targetId);
        cmd.Options.Add(reason);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var targetIdValue = ctx.GetValue(targetId) ?? "";
            var reasonValue = ctx.GetValue(reason) ?? "";

            if (!(int.TryParse(sourceIdValue, out var adrId) && int.TryParse(targetIdValue, out var taskId)))
            {
                stdOut.WriteLine("ADR id and task id should be valid identifiers.");
                return;
            }

            var c = serviceProvider.GetRequiredService<IAdrLink>();
            var result = await c.LinkAdrToTaskAsync(adrId, taskId, reasonValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command UnlinkTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("adr-unlink-task", "Remove a task from an ADR's related tasks (see AdrRecord.RelatedTasks / ADR 00010).");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;

        sourceId.Required = true;
        sourceId.Aliases.Add("-s");
        targetId.Required = true;
        targetId.Aliases.Add("-t");

        cmd.Options.Add(sourceId);
        cmd.Options.Add(targetId);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var targetIdValue = ctx.GetValue(targetId) ?? "";

            if (!(int.TryParse(sourceIdValue, out var adrId) && int.TryParse(targetIdValue, out var taskId)))
            {
                stdOut.WriteLine("ADR id and task id should be valid identifiers.");
                return;
            }

            var c = serviceProvider.GetRequiredService<IAdrLink>();
            var result = await c.RemoveAdrTaskLinkAsync(adrId, taskId);
            stdOut.Write(result);
        });
        return cmd;
    }
}