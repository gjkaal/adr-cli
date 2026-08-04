using Xunit;

namespace Adr.Cli.Sync;

public sealed class WithAdrBodyFormat
{
    [Fact]
    public void Combine_ThenSplit_RoundTripsAllThreeSections()
    {
        var body = AdrBodyFormat.Combine("We need decoupled services.", "Adopt a message bus.", "Operational overhead increases.");

        var (context, decision, consequences) = AdrBodyFormat.Split(body);

        Assert.Equal("We need decoupled services.", context);
        Assert.Equal("Adopt a message bus.", decision);
        Assert.Equal("Operational overhead increases.", consequences);
    }

    [Fact]
    public void Split_BodyWithoutHeaders_TreatsWholeBodyAsContext()
    {
        var (context, decision, consequences) = AdrBodyFormat.Split("Someone rewrote this issue body externally.");

        Assert.Equal("Someone rewrote this issue body externally.", context);
        Assert.Equal(string.Empty, decision);
        Assert.Equal(string.Empty, consequences);
    }
}
