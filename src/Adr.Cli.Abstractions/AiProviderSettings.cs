namespace Adr.Cli;

/// <summary>
/// Configuration for the optional AI provider used to draft ADR proposals. An empty
/// <see cref="Provider" /> means AI generation is not configured, and the registered
/// <c>IAdrProposalGenerator</c> will be a no-op.
/// </summary>
public class AiProviderSettings
{
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the environment variable holding the AI provider's API key. Empty means "use the
    /// provider's own default" (e.g. <c>ADR_CLI_AI_API_KEY</c>) - override this when a machine works
    /// across multiple repositories/contexts that each need a distinct key stored under a distinct
    /// variable name.
    /// </summary>
    public string ApiKeyName { get; set; } = string.Empty;
}
