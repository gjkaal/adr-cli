using System.Collections.Generic;
using System.Threading.Tasks;

using Adr.Cli.Ai;

using McpCore;

namespace Adr.Cli.Services;

/// <summary>
/// Default <see cref="IAdrProposalGenerator" /> registered when no AI provider is configured in
/// adr.config.json. Always fails, so callers fall back to template-only ADR creation without a
/// null check.
/// </summary>
public class NoOpAdrProposalGenerator : IAdrProposalGenerator
{
    public Task<Response<AdrProposal>> GenerateAsync(string title, string context, IReadOnlyList<AdrSummary> existingRecords)
    {
        var response = new Response<AdrProposal>(
            false,
            "AI proposal generation is not configured. Add an \"ai\" section to adr.config.json to enable it - see AI-Setup.md.",
            new AdrProposal());
        return Task.FromResult(response);
    }
}
