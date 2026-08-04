using Adr.Cli.CommandHandlers;

using System.Collections.Generic;

namespace Adr.Cli.Sync.GitHubProjects;

public enum GitHubProjectOwnerType
{
    User,
    Organization
}

/// <summary>
/// Deserialized from the opaque <c>sync.settings</c> payload in adr.config.json when
/// <c>sync.provider</c> is <c>"GitHubProjects"</c>. <see cref="OwnerType" /> is required and never
/// auto-detected, since the caller already knows it from the project's own URL/number.
/// </summary>
public class GitHubProjectsSettings
{
    public GitHubProjectOwnerType OwnerType { get; set; }
    public string Owner { get; set; } = string.Empty;
    public int ProjectNumber { get; set; }

    /// <summary>
    /// Name of the single-select field on the project board that carries workflow status.
    /// </summary>
    public string StatusFieldName { get; set; } = "Status";

    /// <summary>
    /// External single-select option name -> local status, used by <c>task-import</c>. An external
    /// option with no entry here leaves the task's sync state unmapped rather than guessing.
    /// </summary>
    public Dictionary<string, PlanningStatus> ImportStatusMap { get; set; } = new();

    /// <summary>
    /// Local status -> external single-select option name, used by <c>task-export</c>. Configured
    /// independently of <see cref="ImportStatusMap" /> - not derived by inverting it, since several
    /// external options can map to the same local status.
    /// </summary>
    public Dictionary<PlanningStatus, string> ExportStatusMap { get; set; } = new();
}
