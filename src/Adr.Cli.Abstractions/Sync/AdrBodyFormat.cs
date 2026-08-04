namespace Adr.Cli.Sync;

/// <summary>
/// Shared convention for combining an ADR's Context/Decision/Consequences into a single body field
/// (for providers, like GitHub Projects, whose external item only has one content field) and
/// splitting a pulled body back into the three on import. Mirrors <see cref="TaskBodyFormat" /> for
/// Tasks - kept in its own type rather than extended to cover both shapes, since an ADR has three
/// sections against a Task's two. See ADR 00010.
/// </summary>
public static class AdrBodyFormat
{
    private const string ContextHeader = "## Context";
    private const string DecisionHeader = "## Decision";
    private const string ConsequencesHeader = "## Consequences";

    public static string Combine(string context, string decision, string consequences)
    {
        return $"{ContextHeader}\n\n{context}\n\n{DecisionHeader}\n\n{decision}\n\n{ConsequencesHeader}\n\n{consequences}";
    }

    /// <summary>
    /// Best-effort inverse of <see cref="Combine" />. When the three section headers aren't found in
    /// order (e.g. the body was edited externally into free-form text), the whole body becomes
    /// <c>Context</c> and <c>Decision</c>/<c>Consequences</c> are left empty rather than guessing
    /// where a split should go - the same acceptance of a known limitation as
    /// <see cref="TaskBodyFormat.Split" /> makes for Task content (see ADR 00009).
    /// </summary>
    public static (string Context, string Decision, string Consequences) Split(string body)
    {
        var contextIndex = body.IndexOf(ContextHeader, System.StringComparison.Ordinal);
        var decisionIndex = body.IndexOf(DecisionHeader, System.StringComparison.Ordinal);
        var consequencesIndex = body.IndexOf(ConsequencesHeader, System.StringComparison.Ordinal);

        if (contextIndex < 0 || decisionIndex < 0 || consequencesIndex < 0
            || decisionIndex < contextIndex || consequencesIndex < decisionIndex)
        {
            return (body, string.Empty, string.Empty);
        }

        var context = body[(contextIndex + ContextHeader.Length)..decisionIndex].Trim();
        var decision = body[(decisionIndex + DecisionHeader.Length)..consequencesIndex].Trim();
        var consequences = body[(consequencesIndex + ConsequencesHeader.Length)..].Trim();
        return (context, decision, consequences);
    }
}
