using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for verifying and switching which adr.config.json is active for this process.
/// This is the single place that formats <see cref="AdrContextInfo" /> into a response - both the
/// CLI commands and the MCP tools delegate here rather than formatting it themselves.
/// </summary>
public class AdrContext : IAdrContext
{
    private readonly IAdrSettings settings;

    public AdrContext(IAdrSettings settings)
    {
        this.settings = settings;
    }

    public Task<Response> GetContextAsync()
    {
        return Task.FromResult(Format("Current context", settings.CurrentContext));
    }

    public Task<Response> SetContextAsync(string workingDirectory)
    {
        var context = settings.TrySetContext(workingDirectory);
        var response = context.Success
            ? Format("Context set", context)
            : Response.Fail(context.ErrorMessage ?? $"Could not set context to '{workingDirectory}'.");

        return Task.FromResult(response);
    }

    private static Response Format(string verb, AdrContextInfo context)
    {
        var configLabel = context.ConfigFilePath ?? "(none found - using built-in defaults)";
        var aiLabel = context.AiConfigured ? $"connected ({context.AiProvider})" : "not connected";
        return Response.Ok(
            $"{verb}: project=\"{context.ProjectName}\", config={configLabel}, " +
            $"docs={context.DocFolder}, tasks={context.TasksFolder}, ai={aiLabel}.");
    }
}
