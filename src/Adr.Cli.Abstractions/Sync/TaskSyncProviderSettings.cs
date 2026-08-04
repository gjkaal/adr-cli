using System.Text.Json;

namespace Adr.Cli.Sync;

/// <summary>
/// Configuration for the optional task sync connector. An empty <see cref="Provider" /> means no
/// connector is configured, and the registered <c>ITaskSyncProvider</c> will be a no-op. Only one
/// provider is active at a time; <see cref="Settings" /> is deliberately opaque here - Azure DevOps
/// and GitHub Projects scope themselves too differently (organization/project/work-item-type versus
/// owner/project-number/status-field) to share one fixed set of named fields, so each provider
/// implementation deserializes its own settings type from this payload independently.
/// </summary>
public class TaskSyncProviderSettings
{
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Name of the environment variable holding the provider's personal access token. Empty means
    /// "use the provider's own default" (e.g. <c>ADR_CLI_SYNC_PAT</c>) - override this when a
    /// machine works across multiple repositories/contexts that each need a distinct token stored
    /// under a distinct variable name, so the right credential is read for the repository actually
    /// in use rather than whichever one happened to be set last.
    /// </summary>
    public string SyncPatName { get; set; } = string.Empty;

    public JsonElement Settings { get; set; }
}
