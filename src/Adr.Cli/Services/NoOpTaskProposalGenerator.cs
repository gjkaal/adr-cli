using System.Collections.Generic;
using System.Threading.Tasks;

using Adr.Cli.Ai;

using McpCore;

namespace Adr.Cli.Services;

/// <summary>
/// Default <see cref="ITaskProposalGenerator" /> registered when no AI provider is configured in
/// adr.config.json. Always fails, so callers fall back to template-only task creation without a
/// null check.
/// </summary>
public class NoOpTaskProposalGenerator : ITaskProposalGenerator
{
    public Task<Response<TaskProposal>> GenerateAsync(string title, string description, IReadOnlyList<TaskSummary> existingTasks, string templateType)
    {
        var response = new Response<TaskProposal>(
            false,
            "AI proposal generation is not configured. Add an \"ai\" section to adr.config.json to enable it - see AI-Setup.md.",
            new TaskProposal());
        return Task.FromResult(response);
    }
}
