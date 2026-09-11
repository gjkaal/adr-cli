namespace Adr.Cli.Extensions;

/// <summary>
/// Text normalization applied to user- and AI-authored ADR/Task content before it is stored.
/// </summary>
public static class TextSanitizerExtensions
{
    private static readonly char[] TypographicDashes = ['—', '–', '―'];

    /// <summary>
    /// Replace em dash (—), en dash (–), and horizontal bar (―) with a plain ASCII
    /// hyphen. AI-drafted content in particular favors typographic dashes, and this tool's stdio
    /// JSON-RPC pipeline is not guaranteed to round-trip non-ASCII characters correctly on every
    /// platform/console codepage - normalizing dashes to ASCII avoids that class of corruption and
    /// keeps ADR content consistent regardless of how it was authored.
    /// </summary>
    public static string NormalizeDashes(this string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        foreach (var dash in TypographicDashes)
        {
            if (text.Contains(dash))
            {
                text = text.Replace(dash, '-');
            }
        }
        return text;
    }
}
