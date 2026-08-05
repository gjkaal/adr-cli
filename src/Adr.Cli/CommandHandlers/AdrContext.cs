using System.Text;
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

    public Task<Response> SetContextAsync(string workingDirectoryOrProjectName)
    {
        var context = settings.TrySetContext(workingDirectoryOrProjectName);
        if (context.Success)
        {
            return Task.FromResult(Format("Context set", context));
        }

        var message = context.Candidates.Count > 0
            ? FormatCandidates(context)
            : context.ErrorMessage ?? $"Could not set context to '{workingDirectoryOrProjectName}'.";

        return Task.FromResult(Response.Fail(message));
    }

    private static Response Format(string verb, AdrContextInfo context)
    {
        var configLabel = context.ConfigFilePath ?? "(none found - using built-in defaults)";
        var aiLabel = context.AiConfigured ? $"connected ({context.AiProvider})" : "not connected";
        var syncLabel = context.SyncConfigured ? $"connected ({context.SyncProvider})" : "not connected";
        return Response.Ok(
            $"{verb}: project=\"{context.ProjectName}\", config={configLabel}, " +
            $"docs={context.DocFolder}, tasks={context.TasksFolder}, ai={aiLabel}, sync={syncLabel}.");
    }

    private static string FormatCandidates(AdrContextInfo context)
    {
        var sb = new StringBuilder();
        sb.AppendLine(context.ErrorMessage);
        foreach (var candidate in context.Candidates)
        {
            sb.AppendLine($"- \"{candidate.ProjectName}\" -> {candidate.FolderPath}");
        }
        sb.Append("Call adr_set_context again with one of the folder paths or project names above.");
        return sb.ToString();
    }
}
