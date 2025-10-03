using System;
using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrLinkSetup
{
    public static Command LinkCommand(IServiceProvider serviceProvider)
    {
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
            await c.HandleLinkAdrAsync(sourceIdValue, targetIdValue, reasonValue, AdrLinkTypeOperation.Create);
        });
        return cmd;
    }

    public static Command UnLinkCommand(IServiceProvider serviceProvider)
    {
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
            await c.HandleLinkAdrAsync(sourceIdValue, targetIdValue, "remove", AdrLinkTypeOperation.Remove);
        });
        return cmd;
    }
}