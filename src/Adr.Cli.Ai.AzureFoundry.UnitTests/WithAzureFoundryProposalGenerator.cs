using System.Collections.Generic;

using Xunit;

namespace Adr.Cli.Ai.AzureFoundry;

public sealed class WithAzureFoundryProposalGenerator
{
    [Fact]
    public void ParseProposal_WellFormedJson_FormatsConsequencesAsProsAndConsLists()
    {
        var reply = """
            {
                "context": "Services currently call each other synchronously over HTTP.",
                "decision": "Adopt a message bus for service integration.",
                "pros": ["Services are decoupled.", "Retries become easier."],
                "cons": ["Operational overhead increases (mitigate with managed hosting)."]
            }
            """;

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal("Services currently call each other synchronously over HTTP.", proposal.Context);
        Assert.Equal("Adopt a message bus for service integration.", proposal.Decision);
        Assert.Equal(
            "*Pro's:*\n" +
            "- Services are decoupled.\n" +
            "- Retries become easier.\n" +
            "\n" +
            "*Con's:*\n" +
            "- Operational overhead increases (mitigate with managed hosting).",
            proposal.Consequences);
    }

    [Fact]
    public void ParseProposal_PropertyNameCasingDiffersFromModel_StillDeserializes()
    {
        var reply = """{"Context":"Ctx","Decision":"Dec","Pros":["Pro one"],"Cons":["Con one"]}""";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal("Ctx", proposal.Context);
        Assert.Equal("Dec", proposal.Decision);
        Assert.Contains("Pro one", proposal.Consequences);
        Assert.Contains("Con one", proposal.Consequences);
    }

    [Fact]
    public void ParseProposal_MalformedJson_ReturnsEmptyProposal()
    {
        var reply = "I recommend adopting a message bus.";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal(string.Empty, proposal.Context);
        Assert.Equal(string.Empty, proposal.Decision);
        Assert.Equal(string.Empty, proposal.Consequences);
    }

    [Fact]
    public void ParseProposal_NoProsOrCons_ConsequencesIsEmpty()
    {
        var reply = """{"context":"Ctx","decision":"Dec","pros":[],"cons":[]}""";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal(string.Empty, proposal.Consequences);
    }

    [Fact]
    public void IsWellFormed_DistinctNonEmptyFieldsWithProsAndCons_ReturnsTrue()
    {
        var proposal = new AdrProposal
        {
            Context = "Services currently call each other synchronously over HTTP.",
            Decision = "Adopt a message bus for service integration.",
            Consequences = "*Pro's:*\n- Decoupling improves.\n\n*Con's:*\n- Operational overhead increases."
        };

        Assert.True(AzureFoundryProposalGenerator.IsWellFormed(proposal));
    }

    [Theory]
    [InlineData("", "Decision text", "*Pro's:*\n- a\n\n*Con's:*\n- b")]
    [InlineData("Context text", "", "*Pro's:*\n- a\n\n*Con's:*\n- b")]
    [InlineData("Context text", "Decision text", "")]
    public void IsWellFormed_AnyFieldEmpty_ReturnsFalse(string context, string decision, string consequences)
    {
        var proposal = new AdrProposal { Context = context, Decision = decision, Consequences = consequences };

        Assert.False(AzureFoundryProposalGenerator.IsWellFormed(proposal));
    }

    [Fact]
    public void IsWellFormed_DecisionRestatesContext_ReturnsFalse()
    {
        var proposal = new AdrProposal
        {
            Context = "Services call each other synchronously over HTTP.",
            Decision = "Services call each other synchronously over HTTP.",
            Consequences = "*Pro's:*\n- a\n\n*Con's:*\n- b"
        };

        Assert.False(AzureFoundryProposalGenerator.IsWellFormed(proposal));
    }

    [Fact]
    public void IsWellFormed_ConsequencesMissingConsList_ReturnsFalse()
    {
        var proposal = new AdrProposal
        {
            Context = "Services call each other synchronously over HTTP.",
            Decision = "Adopt a message bus for service integration.",
            Consequences = "*Pro's:*\n- Decoupling improves."
        };

        Assert.False(AzureFoundryProposalGenerator.IsWellFormed(proposal));
    }

    [Fact]
    public void SearchExistingRecords_NoKeywords_ReturnsAllRecords()
    {
        var records = new List<AdrSummary>
        {
            new() { RecordId = 1, Title = "Use a message bus", Status = AdrStatus.Accepted, Context = "Decoupling services" },
            new() { RecordId = 2, Title = "Use REST for APIs", Status = AdrStatus.Accepted, Context = "Public API surface" }
        };

        var result = AzureFoundryProposalGenerator.SearchExistingRecords([], records);

        Assert.Contains("Use a message bus", result);
        Assert.Contains("Use REST for APIs", result);
    }

    [Fact]
    public void SearchExistingRecords_MatchingKeyword_ReturnsOnlyMatchingRecords()
    {
        var records = new List<AdrSummary>
        {
            new() { RecordId = 1, Title = "Use a message bus", Status = AdrStatus.Accepted, Context = "Decoupling services" },
            new() { RecordId = 2, Title = "Use REST for APIs", Status = AdrStatus.Accepted, Context = "Public API surface" }
        };

        var result = AzureFoundryProposalGenerator.SearchExistingRecords(["message"], records);

        Assert.Contains("Use a message bus", result);
        Assert.DoesNotContain("Use REST for APIs", result);
    }

    [Fact]
    public void SearchExistingRecords_NoMatches_ReturnsNotFoundMessage()
    {
        var records = new List<AdrSummary>
        {
            new() { RecordId = 1, Title = "Use a message bus", Status = AdrStatus.Accepted, Context = "Decoupling services" }
        };

        var result = AzureFoundryProposalGenerator.SearchExistingRecords(["kubernetes"], records);

        Assert.Equal("No matching ADRs found.", result);
    }
}
