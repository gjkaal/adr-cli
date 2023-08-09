using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;

namespace Adr.Cli.CommandHandlers;

public static class AdrQuerySetup
{
    public static Command ListCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("list", "List all Architecture Decision Records");
        var sortReverse = CommandOptions.SortReverse;
        var verbose = CommandOptions.Verbose;

        cmd.AddOption(sortReverse);
        cmd.AddOption(verbose);
        cmd.SetHandler(async (sortReverse, verbose) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrQuery>();
            await c.ListAdrAsync(sortReverse, verbose);
        }, sortReverse, verbose);

        return cmd;
    }

    public static Command QueryCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("find", "Find Architecture Decision Records");
        var sortReverse = CommandOptions.SortReverse;
        var verbose = CommandOptions.Verbose;
        var includeContent = CommandOptions.IncludeContent;
        var filter = CommandOptions.Filter;

        cmd.AddOption(sortReverse);
        cmd.AddOption(verbose);
        cmd.AddOption(includeContent);
        cmd.AddOption(filter);
        cmd.SetHandler(async (filter, sortReverse, verbose, includeContent) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrQuery>();
            await c.FindAdrAsync(filter, sortReverse, verbose, includeContent);
        }, filter, sortReverse, verbose, includeContent);

        return cmd;
    }
}