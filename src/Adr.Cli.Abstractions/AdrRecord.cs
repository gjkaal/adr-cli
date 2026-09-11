using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Adr.Cli;

public abstract class AdrRecordBase
{
    public DateTime DateTime { get; set; } = DateTime.Today;
    public string FileName { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class AdrRecord : AdrRecordBase, ICloneable
{
    public AdrStatus Status { get; set; } = AdrStatus.New;

    [JsonIgnore]
    public AdrRecord? SuperSedes { get; set; }

    public TemplateType TemplateType { get; set; }
    public string Context { get; set; } = string.Empty;

    [JsonIgnore]
    public string Decision { get; set; } = string.Empty;

    [JsonIgnore]
    public string Consequences { get; set; } = string.Empty;

    public Dictionary<int, string> References { get; set; } = new();

    /// <summary>
    /// Tasks that belong to this ADR (task id -> remark). Populated via <c>adr-link-task</c>, distinct
    /// from <see cref="References" /> (ADR-to-ADR only) since Tasks and ADRs each number their own
    /// records from 1 and a shared dictionary couldn't disambiguate the two. Used by <c>adr-export</c>
    /// to attach each related task as a GitHub sub-issue of this ADR's Issue - see ADR 00010.
    /// </summary>
    public Dictionary<int, string> RelatedTasks { get; set; } = new();

    /// <summary>
    /// Per-provider synchronization links for this ADR. Only one provider is active per repository at
    /// a time (see <see cref="Sync.SyncLink.Provider" />) - see ADR 00010.
    /// </summary>
    public List<Sync.SyncLink> SyncLinks { get; set; } = [];

    public object Clone()
    {
        var result = new AdrRecord
        {
            DateTime = DateTime,
            FileName = FileName,
            RecordId = RecordId,
            Status = Status,
            SuperSedes = SuperSedes,
            TemplateType = TemplateType,
            Title = Title,
            Context = Context
        };
        foreach (var key in References.Keys)
        {
            var reference = References[key];
            result.References.Add(key, reference);
        }
        foreach (var key in RelatedTasks.Keys)
        {
            result.RelatedTasks.Add(key, RelatedTasks[key]);
        }
        foreach (var link in SyncLinks)
        {
            result.SyncLinks.Add(new Sync.SyncLink
            {
                Provider = link.Provider,
                ExternalScope = link.ExternalScope,
                ExternalId = link.ExternalId,
                ExternalContentType = link.ExternalContentType,
                ExternalUrl = link.ExternalUrl,
                SyncState = link.SyncState,
                LastSyncedAt = link.LastSyncedAt
            });
        }
        return result;
    }
}
