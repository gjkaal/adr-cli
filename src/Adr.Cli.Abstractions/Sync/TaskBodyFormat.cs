namespace Adr.Cli.Sync;

/// <summary>
/// Shared convention for combining a task's Description and Details into a single body field (for
/// providers, like GitHub Projects, whose external item only has one content field) and splitting a
/// pulled body back into the two on import. Kept in one place so every provider and the shared
/// import/export orchestration agree on the same separator - a provider defining its own private
/// combine logic would make a safe content pull (see ADR 00009) impossible to split back correctly.
/// </summary>
public static class TaskBodyFormat
{
    private const string Separator = "\n\n---\n\n";

    public static string Combine(string description, string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return description;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return details;
        }

        return $"{description}{Separator}{details}";
    }

    /// <summary>
    /// Best-effort inverse of <see cref="Combine" />. When the separator isn't found (e.g. the body
    /// was edited externally into free-form text), the whole body becomes <c>Description</c> and
    /// <c>Details</c> is left empty rather than guessing where a split should go.
    /// </summary>
    public static (string Description, string Details) Split(string body)
    {
        var separatorIndex = body.IndexOf(Separator, System.StringComparison.Ordinal);
        return separatorIndex < 0
            ? (body, string.Empty)
            : (body[..separatorIndex], body[(separatorIndex + Separator.Length)..]);
    }
}
