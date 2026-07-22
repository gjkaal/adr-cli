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
}
