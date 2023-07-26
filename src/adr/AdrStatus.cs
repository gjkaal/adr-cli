namespace adr
{
    /// <summary>
    /// Controlled list for ADR / ADC record status
    /// </summary>
    public enum AdrStatus
    {
        /// <summary>
        /// This is a new record with some default content.
        /// </summary>
        New = 0,
        /// <summary>
        /// This is a proposal and still needs to be modified before if can be reviewed.
        /// </summary>
        Proposed = 1,
        /// <summary>
        /// This is the final draft for the ADR and it's ready for review. 
        /// </summary>
        Final = 2,
        /// <summary>
        /// The accepted records should not be modified. The only state it can get is 'obsolete'.
        /// </summary>
        Accepted = 3,
        /// <summary>
        /// If a Adr is invalid or could not be deserialized, the state will be 'error'
        /// </summary>
        Error = 254,
        /// <summary>
        /// An obsolete ADR is no longer applicable.
        /// </summary>
        Obsolete = 255
    }
}