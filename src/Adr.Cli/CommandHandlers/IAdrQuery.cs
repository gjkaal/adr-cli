using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for listing ADR's and searching data
/// </summary>
public interface IAdrQuery
{
    /// <summary>
    /// Show a list with all ADR titles with their creation date and current state.
    /// </summary>
    /// <param name="sortReverse">Show ADR in reverse order (newest first.</param>
    /// <param name="verbose">Show ADR with more details.</param>
    /// <returns>integer indicating success or failure</returns>
    Task<int> ListAdrAsync(bool sortReverse, bool verbose);

    /// <summary>
    /// Show a list with ADR titles using a filter on title and content.
    /// </summary>
    /// <param name="filter">A set of words that should be present in the ADR.</param>
    /// <param name="includeContent">search words in metadata and in content (slower).</param>
    /// <param name="sortReverse">Show ADR in reverse order (newest first.</param>
    /// <param name="verbose">Show ADR with more details.</param>
    /// <returns>integer indicating success or failure</returns>
    Task<int> FindAdrAsync(string filter, bool sortReverse, bool verbose, bool includeContent);
}
