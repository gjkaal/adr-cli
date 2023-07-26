namespace adr
{
    /// <summary>
    /// Controlled set for the type of template.
    /// </summary>
    public enum TemplateType
    {
        /// <summary>
        /// Initialization can occur only once. Initializing an ADR repository multiple times
        /// will yield an exception.
        /// </summary>
        Init = 0,
        /// <summary>
        /// Use the Architecture Decision template.
        /// </summary>
        Ad = 1,
        /// <summary>
        /// Use the Architecture Significant Requirement template.
        /// </summary>
        Asr = 2,
        /// <summary>
        /// Add a revision document for either an AD or ASR
        /// </summary>
        Revision = 3,
    }
}