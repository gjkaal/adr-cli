using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrContextSetup
{
    /// <summary>
    /// Define the command for showing which adr.config.json is currently active.
    /// </summary>
    /// <remarks>
    /// There is no CLI equivalent for adr_set_context: a CLI invocation is a fresh process that
    /// already resolves settings from its actual current directory, and an in-memory context
    /// switch can't outlive that single process anyway - "set" only has meaning for the long-lived
    /// MCP server, where the process cwd is fixed once at startup and unrelated to the caller's
    /// working directory. To point the CLI at a different repository, run it from that directory.
    /// </remarks>
    public static Command GetContextCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("context", "Show which adr.config.json is currently active for this directory");

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrContext>();
            var result = await c.GetContextAsync();
            stdOut.Write(result);
        });
        return cmd;
    }
}
