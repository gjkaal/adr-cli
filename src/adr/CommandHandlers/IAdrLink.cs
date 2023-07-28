using System.Threading.Tasks;
using static adr.CommandHandlers.AdrLink;

namespace adr.CommandHandlers;

/// <summary>
/// Command handler for managing links between adr records.
/// </summary>
public interface IAdrLink
{
    /// <summary>
    /// Wrapper for link or remove with parameter validation.
    /// </summary>
    /// <param name="sourceId">The record that extends another record.</param>
    /// <param name="remark">The keyword that explains the link.</param>
    /// <param name="targetId">The record that is linked to this record.</param>
    /// <param name="operation">The type of operation</param>
    /// <returns></returns>
    Task<int> HandleLinkAdrAsync(string sourceId, string targetId, string remark, AdrLinkTypeOperation operation);

    /// <summary>
    /// Add a link between two records.
    /// Examples:
    /// <list type="bullet">
    /// <item>link 5 Amend 4</item>
    /// <item>link 2 Replaced-by 5</item>
    /// </list>
    /// </summary>
    /// <param name="sourceId">The record that extends another record.</param>
    /// <param name="remark">The keyword that explains the link.</param>
    /// <param name="targetId">The record that is linked to this record.</param>
    /// <returns></returns>
    Task<int> LinkAdrAsync(int sourceId, int targetId, string remark);

    /// <summary>
    /// Remove all links from the source to the target (reverse links are not removed).
    /// </summary>
    /// <param name="sourceId">The record that extends another record.</param>
    /// <param name="targetId">The record that is linked to this record.</param>
    /// <returns></returns>
    Task<int> RemoveLinkAsync(int sourceId, int targetId);
}

/// <summary>
/// Enumerate the options for the generic handler.
/// </summary>
public enum AdrLinkTypeOperation
{
    Create,
    Remove
}
