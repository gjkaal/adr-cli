using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for initializing a new ADR folder.
/// </summary>
public interface IAdrNew
{
    /// <summary>
    /// Initialize an AD record or a reuirement at the current location.
    /// </summary>
    /// <param name="title">The title for the adr.</param>
    /// <param name="isRequirement">This is a critical requirement.</param>
    /// <param name="revisionForRecord">This AD is a revision for a previous record.</param>
    /// <param name="context">The context for this decision.</param>
    /// <returns>integer indicating success or failure</returns>
    Task<int> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context);

    /// <summary>
    /// Copy an existing ADR to a new ADR with or without a revision remark.
    /// </summary>
    /// <param name="recordId">An existing record</param>
    /// <param name="isRevision">Defie the new record as a revision for the previous record.</param>
    /// <returns></returns>
    Task<int> CopyAdrAsync(int recordId, bool isRevision);
}
