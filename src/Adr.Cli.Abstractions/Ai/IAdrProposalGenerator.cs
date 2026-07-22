using System.Collections.Generic;
using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.Ai;

/// <summary>
/// A condensed view of an existing ADR, used as grounding context when drafting a new proposal.
/// </summary>
public class AdrSummary
{
    public int RecordId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AdrStatus Status { get; set; }
    public string Context { get; set; } = string.Empty;
}

/// <summary>
/// AI-drafted content for the Context, Decision, and Consequences sections of an ADR.
/// </summary>
public class AdrProposal
{
    public string Context { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Consequences { get; set; } = string.Empty;
}

/// <summary>
/// Drafts Context/Decision/Consequences content for a new ADR from its title and context.
/// Implementations are swappable per <see cref="Adr.Cli.AiProviderSettings" />; when AI is not
/// configured, the registered implementation is a no-op that always fails, so callers never need a
/// null check.
/// </summary>
public interface IAdrProposalGenerator
{
    /// <summary>
    /// Draft a proposal for a new ADR.
    /// </summary>
    /// <param name="title">
    /// The title for the new ADR.
    /// </param>
    /// <param name="context">
    /// The user-authored context for the new ADR, if any. When empty, the generator drafts one
    /// instead of leaving it blank.
    /// </param>
    /// <param name="existingRecords">
    /// Condensed summaries of existing ADRs, so the generator can stay consistent with (or point out
    /// conflicts with) prior decisions.
    /// </param>
    /// <param name="templateType">
    /// The ADR's <c>TemplateType</c> (e.g. "Ad", "Asr", "Revision"), used to look up the matching
    /// on-disk template as a structure/tone example for the model. Best-effort - implementations
    /// should tolerate a missing template file.
    /// </param>
    Task<Response<AdrProposal>> GenerateAsync(string title, string context, IReadOnlyList<AdrSummary> existingRecords, string templateType);
}
