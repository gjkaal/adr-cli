using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Adr.Cli.Sync;

/// <summary>
/// Outcome of comparing local content against a remote item's embedded hash marker.
/// </summary>
public enum ContentSyncOutcome
{
    /// <summary>
    /// Content is already identical (or no meaningful difference) on both sides.
    /// </summary>
    NoChange,

    /// <summary>
    /// Import only: remote content changed since the last sync and local did not - safe to adopt
    /// remote's content locally and refresh the marker.
    /// </summary>
    Pull,

    /// <summary>
    /// Content diverged on a side that was expected to be stable since the last sync - refuse to
    /// touch either side.
    /// </summary>
    Mismatch
}

/// <summary>
/// Shared, provider-agnostic logic for detecting content divergence between a task's local
/// title/description and the corresponding remote item, via a trailing <c>[HASH:...]</c> marker
/// line embedded only in the remote copy (never stored locally - the local hash is always computed
/// fresh from whatever the file currently contains, since it may be edited by any other process).
/// Every provider that pushes task content must use this same hash algorithm so a marker written by
/// one provider round-trips correctly. See ADR 00008.
/// </summary>
public static class ContentSyncMarker
{
    private const string MarkerPrefix = "[HASH:";
    private const string MarkerSuffix = "]";

    /// <summary>
    /// Combined hash of <paramref name="title" /> and <paramref name="body" /> with all whitespace
    /// characters removed before hashing, so purely cosmetic differences (line endings, trailing
    /// spaces, reformatting) never register as a content change. Title and body are hashed together
    /// so a title-only edit changes the hash even when the body is untouched.
    /// </summary>
    public static string ComputeHash(string title, string body)
    {
        // A non-whitespace separator disambiguates the title/body boundary (whitespace alone can't,
        // since it's stripped) - e.g. Title="AB"+Body="C" must not hash the same as Title="A"+Body="BC".
        var normalized = StripWhitespace(title) + "" + StripWhitespace(body);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hashBytes);
    }

    private static string StripWhitespace(string text)
    {
        return new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    /// <summary>
    /// Appends the marker line to <paramref name="body" /> for pushing to the remote side. Local
    /// storage never carries this line.
    /// </summary>
    public static string AppendMarker(string body, string hash)
    {
        return $"{body}\n{MarkerPrefix}{hash}{MarkerSuffix}";
    }

    /// <summary>
    /// Splits a remote body into its content and embedded hash, if present. A missing marker (e.g. a
    /// task linked without ever being pushed through this mechanism) returns a <see langword="null" />
    /// hash - callers should treat that as "no established baseline" rather than a mismatch.
    /// </summary>
    public static (string Content, string? Hash) SplitMarker(string bodyWithMarker)
    {
        var lastLineBreak = bodyWithMarker.LastIndexOf('\n');
        var lastLine = (lastLineBreak >= 0 ? bodyWithMarker[(lastLineBreak + 1)..] : bodyWithMarker).Trim();

        if (lastLine.StartsWith(MarkerPrefix, StringComparison.Ordinal) && lastLine.EndsWith(MarkerSuffix, StringComparison.Ordinal))
        {
            var hash = lastLine[MarkerPrefix.Length..^MarkerSuffix.Length];
            var content = lastLineBreak >= 0 ? bodyWithMarker[..lastLineBreak] : string.Empty;
            return (content, hash);
        }

        return (bodyWithMarker, null);
    }

    /// <summary>
    /// Export-side check: is the remote item unchanged since the last sync, so a push won't silently
    /// discard someone else's edit? True when the remote's own content still hashes to its own
    /// embedded marker, or when there is no marker yet (nothing to protect).
    /// </summary>
    public static bool IsRemoteSafeToOverwrite(string remoteTitle, string remoteBodyWithMarker)
    {
        var (content, hash) = SplitMarker(remoteBodyWithMarker);
        return hash == null || ComputeHash(remoteTitle, content) == hash;
    }

    /// <summary>
    /// Import-side decision: given local content and the remote item's title/body (with marker),
    /// determine whether nothing changed, remote can be safely pulled into local, or the two sides
    /// have diverged and need manual reconciliation.
    /// </summary>
    public static ContentSyncOutcome ResolveImport(string localTitle, string localBody, string remoteTitle, string remoteBodyWithMarker, out string pulledTitle, out string pulledBody)
    {
        var (remoteContent, embeddedHash) = SplitMarker(remoteBodyWithMarker);
        pulledTitle = remoteTitle;
        pulledBody = remoteContent;

        var localHash = ComputeHash(localTitle, localBody);
        var remoteHash = ComputeHash(remoteTitle, remoteContent);

        if (remoteHash == localHash)
        {
            return ContentSyncOutcome.NoChange;
        }

        if (embeddedHash != null && localHash != embeddedHash)
        {
            // Local moved since the last sync - whatever remote did, we can't tell whose change
            // should win, so don't guess.
            return ContentSyncOutcome.Mismatch;
        }

        // Local is unchanged since the last sync (or there was never a baseline to compare against),
        // and remote differs from local - safe to adopt remote's content.
        return ContentSyncOutcome.Pull;
    }
}
