using System.Threading.Tasks;

namespace adr.CommandHandlers;

/// <summary>
/// Command handler for initializing a new ADR folder.
/// </summary>
public interface IAdrNew
{
    /// <summary>
    /// Initialize an ADR set at the current location.
    /// </summary>
    /// <param name="title">The title for the adr.</param>
    /// <param name="isRequirement">This is a critical requirement.</param>
    /// <param name="revisionForRecord">This AD is a revision for a previous record.</param>
    /// <param name="context">The context for this decision.</param>
    /// <returns>integer indicating success or failure</returns>
    Task<int> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context);
}
