namespace Adr.Cli.Sync;

/// <summary>
/// One item found on the external provider's board/list during discovery, before it's been matched
/// against local tasks. Used to adopt remote-originated items that have no local counterpart yet -
/// see <see cref="ITaskSyncProvider.DiscoverItemsAsync" />.
/// </summary>
public class DiscoveredExternalItem
{
    public string ExternalScope { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Raw content, with the trailing <c>[HASH:...]</c> marker (see <see cref="ContentSyncMarker" />)
    /// already stripped if one was present.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item already carried a content-sync hash marker. An item without one has never
    /// been pushed through this tool - a natural, though not certain, signal that it originated on
    /// the remote side rather than as a task this tool created.
    /// </summary>
    public bool HasMarker { get; set; }
}
