using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrQuerySetup
{
    public static Command ListCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("list", "List all Architecture Decision Records");
        var sortReverse = CommandOptions.SortReverse;
        var verbose = CommandOptions.Verbose;

        cmd.Options.Add(sortReverse);
        cmd.Options.Add(verbose);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sortReverseValue = ctx.GetValue(sortReverse);
            var verboseValue = ctx.GetValue(verbose);

            var c = serviceProvider.GetRequiredService<IAdrQuery>();
            var result = await c.ListAdrAsync(sortReverseValue, verboseValue);
            stdOut.Write(result);
        });

        return cmd;
    }

    public static Command QueryCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("find", "Find Architecture Decision Records");
        var sortReverse = CommandOptions.SortReverse;
        var verbose = CommandOptions.Verbose;
        var includeContent = CommandOptions.IncludeContent;
        var filter = CommandOptions.Filter;

        cmd.Options.Add(sortReverse);
        cmd.Options.Add(verbose);
        cmd.Options.Add(includeContent);
        cmd.Options.Add(filter);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sortReverseValue = ctx.GetValue(sortReverse);
            var verboseValue = ctx.GetValue(verbose);
            var includeContentValue = ctx.GetValue(includeContent);
            var filterValue = ctx.GetValue(filter) ?? "";

            var c = serviceProvider.GetRequiredService<IAdrQuery>();
            var result = await c.FindAdrAsync(filterValue, sortReverseValue, verboseValue, includeContentValue);
            stdOut.Write(result);
        });

        return cmd;
    }
}