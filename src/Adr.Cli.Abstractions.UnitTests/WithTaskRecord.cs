using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Adr.Cli.CommandHandlers;

using Xunit;

namespace Adr.Cli;

public sealed class WithTaskRecord
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void SerializeThenDeserialize_RoundTripsMetadataFields()
    {
        var record = new TaskRecord
        {
            RecordId = 7,
            Title = "Write the setup guide",
            Status = PlanningStatus.Active,
            DueDate = new DateTime(2026, 8, 1),
            Description = "Document the AI setup steps.",
            Details = "Should not be serialized"
        };
        record.Related.Add(3, "Depends on");
        record.Logs.Add(new StatusUpdate { DateTime = DateTime.Today, Status = PlanningStatus.Active, Justification = "Started" });

        var json = JsonSerializer.Serialize(record, JsonOptions);
        var result = JsonSerializer.Deserialize<TaskRecord>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(record.RecordId, result!.RecordId);
        Assert.Equal(record.Title, result.Title);
        Assert.Equal(record.Status, result.Status);
        Assert.Equal(record.DueDate, result.DueDate);
        Assert.Equal(record.Description, result.Description);
        Assert.Equal(record.Related, result.Related);
        Assert.Single(result.Logs);
        Assert.DoesNotContain("Details", json);
    }

    [Fact]
    public void Clone_CopiesFieldsAndRelated_IntoANewInstance()
    {
        var record = new TaskRecord
        {
            RecordId = 9,
            Title = "Original task",
            Description = "Some description"
        };
        record.Related.Add(4, "Blocks");

        var clone = (TaskRecord)record.Clone();

        Assert.NotSame(record, clone);
        Assert.Equal(record.RecordId, clone.RecordId);
        Assert.Equal(record.Title, clone.Title);
        Assert.Equal(record.Description, clone.Description);
        Assert.Equal(record.Related, clone.Related);
    }
}
