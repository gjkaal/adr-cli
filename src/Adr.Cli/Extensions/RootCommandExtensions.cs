using System.Collections.Generic;
using System.CommandLine;

namespace Adr.Cli.Extensions;

internal static class RootCommandExtensions
{
    public static RootCommand AddRange(this RootCommand rootCommand, IEnumerable<Command> commands)
    {
        foreach (var command in commands)
        {
            rootCommand.AddCommand(command);
        }
        return rootCommand;
    }
}
