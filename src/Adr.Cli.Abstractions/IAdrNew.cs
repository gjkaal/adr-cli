using System.Threading.Tasks;

using McpCore;

namespace Adr.Cli.CommandHandlers;

/// <summary>
/// Command handler for initializing a new ADR folder.
/// </summary>
public interface IAdrNew
{
    /// <summary>
    /// Initialize an AD record or a reuirement at the current location.
    /// </summary>
    /// <param name="title">
    /// The title for the adr.
    /// </param>
    /// <param name="isRequirement">
    /// This is a critical requirement.
    /// </param>
    /// <param name="revisionForRecord">
    /// This AD is a revision for a previous record.
    /// </param>
    /// <param name="context">
    /// The context for this decision.
    /// </param>
    /// <param name="useAi">
    /// Draft the Decision and Consequences sections using the configured AI provider. Ignored (a
    /// no-op) when no provider is configured; on any AI failure the ADR is still created without
    /// AI content. When null, defaults to true if an AI provider is configured and false otherwise,
    /// so callers don't need to remember the flag on every call.
    /// </param>
    /// <returns>
    /// integer indicating success or failure
    /// </returns>
    Task<Response> NewAdrAsync(string title, bool isRequirement, string revisionForRecord, string context, bool? useAi);

    /// <summary>
    /// Copy an existing ADR to a new ADR with or without a revision remark.
    /// </summary>
    /// <param name="sourceId">
    /// A numeric reference to an existing ADR.
    /// </param>
    /// <param name="isRevision">
    /// Defie the new record as a revision for the previous record.
    /// </param>
    /// <returns>
    /// </returns>
    Task<Response> CopyAdrAsync(string sourceId, bool isRevision);

    /// <summary>
    /// Replace the Decision and/or Consequences section of an existing ADR's markdown, in place -
    /// a convenience alternative to hand-editing the .md file directly. Decision/Consequences are
    /// markdown-only (never stored in the .json metadata), so this never touches metadata and never
    /// requires a follow-up sync.
    /// </summary>
    /// <param name="recordId">
    /// The existing ADR's ID.
    /// </param>
    /// <param name="decision">
    /// Replacement text for the Decision section. Null/omitted leaves it unchanged.
    /// </param>
    /// <param name="consequences">
    /// Replacement text for the Consequences section. Null/omitted leaves it unchanged.
    /// </param>
    /// <returns>
    /// A failure if the record doesn't exist or neither parameter was supplied.
    /// </returns>
    Task<Response> UpdateContentAsync(int recordId, string? decision, string? consequences);
}
