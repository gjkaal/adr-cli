using Adr.Cli.CommandHandlers;
using Adr.Cli.Sync;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Adr.Cli;

public class StatusUpdate
{
    public DateTime DateTime { get; set; }
    public PlanningStatus Status { get; set; } = PlanningStatus.None;
    public string Justification { get; set; } = string.Empty;
}

public class TaskRecord : AdrRecordBase, ICloneable
{
    public DateTime? DueDate { get; set; }
    public PlanningStatus Status { get; set; } = PlanningStatus.New;
    public string Description { get; set; } = string.Empty;
    [JsonIgnore]
    public string Prerequisits { get; set; } = string.Empty;

    [JsonIgnore]
    public string Details { get; set; } = string.Empty;

    public Dictionary<int, string> Related { get; set; } = new();
    public List<StatusUpdate> Logs { get; set; } = [];

    /// <summary>
    /// Per-provider synchronization links for this task. Only one provider is active per
    /// repository at a time (see <see cref="Sync.SyncLink.Provider" />), but the list can carry
    /// an inert link left over from a previously-configured provider.
    /// </summary>
    public List<SyncLink> SyncLinks { get; set; } = [];

    public object Clone()
    {
        var result = new TaskRecord
        {
            DateTime = DateTime,
            DueDate = DueDate,
            FileName = FileName,
            RecordId = RecordId,
            Title = Title,
            Description = Description,
            Details = Details
        };
        foreach (var key in Related.Keys)
        {
            var reference = Related[key];
            result.Related.Add(key, reference);
        }
        foreach (var link in SyncLinks)
        {
            result.SyncLinks.Add(new SyncLink
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
