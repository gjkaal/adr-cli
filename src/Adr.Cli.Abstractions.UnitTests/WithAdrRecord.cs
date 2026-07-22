using System.Text.Json;
using System.Text.Json.Serialization;

using Xunit;

namespace Adr.Cli;

public sealed class WithAdrRecord
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void SerializeThenDeserialize_RoundTripsMetadataFields()
    {
        var record = new AdrRecord
        {
            RecordId = 3,
            Title = "Use a message bus",
            Status = AdrStatus.Accepted,
            TemplateType = TemplateType.Ad,
            Context = "We need decoupled services.",
            Decision = "Adopt a message bus.",
            Consequences = "Operational overhead increases."
        };
        record.References.Add(1, "Amends");

        var json = JsonSerializer.Serialize(record, JsonOptions);
        var result = JsonSerializer.Deserialize<AdrRecord>(json, JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(record.RecordId, result!.RecordId);
        Assert.Equal(record.Title, result.Title);
        Assert.Equal(record.Status, result.Status);
        Assert.Equal(record.TemplateType, result.TemplateType);
        Assert.Equal(record.Context, result.Context);
        Assert.Equal(record.References, result.References);
    }

    [Fact]
    public void Serialize_DoesNotIncludeDecisionOrConsequences()
    {
        var record = new AdrRecord
        {
            RecordId = 1,
            Title = "Title",
            Decision = "Should not be serialized",
            Consequences = "Should not be serialized either"
        };

        var json = JsonSerializer.Serialize(record, JsonOptions);

        Assert.DoesNotContain("Decision", json);
        Assert.DoesNotContain("Consequences", json);
    }

    [Fact]
    public void Clone_CopiesFieldsAndReferences_IntoANewInstance()
    {
        var record = new AdrRecord
        {
            RecordId = 5,
            Title = "Original",
            Context = "Some context"
        };
        record.References.Add(2, "Related");

        var clone = (AdrRecord)record.Clone();

        Assert.NotSame(record, clone);
        Assert.Equal(record.RecordId, clone.RecordId);
        Assert.Equal(record.Title, clone.Title);
        Assert.Equal(record.Context, clone.Context);
        Assert.Equal(record.References, clone.References);
    }
}
