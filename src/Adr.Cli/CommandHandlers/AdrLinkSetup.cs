using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;

namespace Adr.Cli.CommandHandlers;

public static class AdrLinkSetup
{
    public static Command LinkCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("link", "Link 2 ADR's for ammend / clarify or some other reason");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;
        var reason = CommandOptions.Reason;

        sourceId.IsRequired = true;
        sourceId.AddAlias("-s");

        targetId.AddAlias("-t");
        reason.AddAlias("-r");

        cmd.AddOption(sourceId);
        cmd.AddOption(targetId);
        cmd.AddOption(reason);

        cmd.SetHandler(async (sourceId, targetId, reason) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrLink>();
            await c.HandleLinkAdrAsync(sourceId, targetId, reason, AdrLinkTypeOperation.Create);
        }, sourceId, targetId, reason);
        return cmd;
    }

    public static Command UnLinkCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("rlink", "Remove all links from one ADR to another");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;

        sourceId.AddAlias("-s");
        targetId.AddAlias("-t");

        cmd.AddOption(sourceId);
        cmd.AddOption(targetId);

        cmd.SetHandler(async (sourceId, targetId) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrLink>();
            await c.HandleLinkAdrAsync(sourceId, targetId, "remove", AdrLinkTypeOperation.Remove);
        }, sourceId, targetId);
        return cmd;
    }
}