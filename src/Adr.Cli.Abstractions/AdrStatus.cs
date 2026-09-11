namespace Adr.Cli;

/// <summary>
/// Controlled list for ADR / ADC record status
/// </summary>
public enum AdrStatus
{
    /// <summary>
    /// No status has been assigned. This is the CLR default for the enum type and must never be a
    /// record's real status - see AdrRecord.Status, which always defaults to New instead. Keeping
    /// this reserved as the zero value stops System.Text.Json's WhenWritingDefault from silently
    /// dropping a legitimate status (New used to be 0, so every newly created ADR's Status field
    /// vanished from its .json on write).
    /// </summary>
    None = 0,

    /// <summary>
    /// This is a new record with some default content.
    /// </summary>
    New = 1,

    /// <summary>
    /// This is a proposal and still needs to be modified before if can be reviewed.
    /// </summary>
    Proposed = 2,

    /// <summary>
    /// This is the final draft for the ADR and it's ready for review.
    /// </summary>
    Final = 3,

    /// <summary>
    /// The accepted records should not be modified. The only state it can get is 'obsolete'.
    /// </summary>
    Accepted = 4,

    /// <summary>
    /// If a Adr is invalid or could not be deserialized, the state will be 'error'
    /// </summary>
    Error = 5,

    /// <summary>
    /// An obsolete ADR is no longer applicable.
    /// </summary>
    Obsolete = 6
}
