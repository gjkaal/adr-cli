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

    /// <summary>
    /// Repository ADRs are synced to as real Issues, and related tasks are promoted into, in
    /// "owner/repo" form. Required for <c>adr-export</c>/<c>adr-import</c> - not needed for plain
    /// task sync, since task export never creates anything beyond a Draft Issue. See ADR 00010.
    /// </summary>
    public string TargetRepository { get; set; } = string.Empty;

    /// <summary>
    /// Name of the single-select field on the project board that carries ADR workflow status.
    /// Deliberately separate from <see cref="StatusFieldName" /> so the ADR and task status
    /// vocabularies never mix in one field's option list. See ADR 00010.
    /// </summary>
    public string AdrStatusFieldName { get; set; } = "ADR Status";

    /// <summary>
    /// External single-select option name -> local <see cref="AdrStatus" />, used by
    /// <c>adr-import</c>. Configured independently of the task status maps.
    /// </summary>
    public Dictionary<string, AdrStatus> AdrImportStatusMap { get; set; } = new();

    /// <summary>
    /// Local <see cref="AdrStatus" /> -> external single-select option name, used by
    /// <c>adr-export</c>. Configured independently of <see cref="AdrImportStatusMap" />.
    /// </summary>
    public Dictionary<AdrStatus, string> AdrExportStatusMap { get; set; } = new();
}
