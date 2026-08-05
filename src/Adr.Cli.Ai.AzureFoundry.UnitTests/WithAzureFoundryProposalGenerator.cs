using System.Collections.Generic;

using Xunit;

namespace Adr.Cli.Ai.AzureFoundry;

public sealed class WithAzureFoundryProposalGenerator
{
    [Fact]
    public void ParseContext_WellFormedJson_ReturnsContext()
    {
        var reply = """{"context": "Services currently call each other synchronously over HTTP."}""";

        var context = AzureFoundryProposalGenerator.ParseContext(reply);

        Assert.Equal("Services currently call each other synchronously over HTTP.", context);
    }

    [Fact]
    public void ParseContext_MalformedJson_ReturnsEmpty()
    {
        var context = AzureFoundryProposalGenerator.ParseContext("I recommend adopting a message bus.");

        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public void ParseDecision_PropertyNameCasingDiffersFromModel_StillDeserializes()
    {
        var reply = """{"Decision":"Adopt a message bus for service integration."}""";

        var decision = AzureFoundryProposalGenerator.ParseDecision(reply);

        Assert.Equal("Adopt a message bus for service integration.", decision);
    }

    [Fact]
    public void ParseDecision_MalformedJson_ReturnsEmpty()
    {
        var decision = AzureFoundryProposalGenerator.ParseDecision("not json");

        Assert.Equal(string.Empty, decision);
    }

    [Fact]
    public void ParseConsequences_WellFormedJson_ReturnsProsAndCons()
    {
        var reply = """
            {
                "pros": ["Services are decoupled.", "Retries become easier."],
                "cons": ["Operational overhead increases (mitigate with managed hosting)."]
            }
            """;

        var (pros, cons) = AzureFoundryProposalGenerator.ParseConsequences(reply);

        Assert.Equal(["Services are decoupled.", "Retries become easier."], pros);
        Assert.Equal(["Operational overhead increases (mitigate with managed hosting)."], cons);
    }

    [Fact]
    public void ParseConsequences_MalformedJson_ReturnsEmptyLists()
    {
        var (pros, cons) = AzureFoundryProposalGenerator.ParseConsequences("not json");

        Assert.Empty(pros);
        Assert.Empty(cons);
    }

    [Fact]
    public void FormatConsequences_ProsAndCons_RendersAsMarkdownLists()
    {
        var formatted = AzureFoundryProposalGenerator.FormatConsequences(
            ["Services are decoupled.", "Retries become easier."],
            ["Operational overhead increases (mitigate with managed hosting)."]);

        Assert.Equal(
            "*Pro's:*\n" +
            "- Services are decoupled.\n" +
            "- Retries become easier.\n" +
            "\n" +
            "*Con's:*\n" +
            "- Operational overhead increases (mitigate with managed hosting).",
            formatted);
    }

    [Fact]
    public void FormatConsequences_NoProsOrCons_ReturnsEmpty()
    {
        var formatted = AzureFoundryProposalGenerator.FormatConsequences([], []);

        Assert.Equal(string.Empty, formatted);
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

    /// <summary>
    /// Regression test: a schema-valid completion with an empty cons array (or pros array) used to
    /// pass IsWellFormed, because it only checked for the "Con's:"/"Pro's:" header substrings, which
    /// FormatConsequences still emits even with zero items under them. An empty list is the same
    /// failure as an empty field entirely - content that looks present but answers nothing.
    /// </summary>
    [Fact]
    public void IsWellFormed_ConsequencesHasHeaderButNoConsItems_ReturnsFalse()
    {
        var proposal = new AdrProposal
        {
            Context = "Services call each other synchronously over HTTP.",
            Decision = "Adopt a message bus for service integration.",
            Consequences = AzureFoundryProposalGenerator.FormatConsequences(["Decoupling improves."], [])
        };

        Assert.False(AzureFoundryProposalGenerator.IsWellFormed(proposal));
    }

    [Fact]
    public void IsWellFormed_ConsequencesHasHeaderButNoProsItems_ReturnsFalse()
    {
        var proposal = new AdrProposal
        {
            Context = "Services call each other synchronously over HTTP.",
            Decision = "Adopt a message bus for service integration.",
            Consequences = AzureFoundryProposalGenerator.FormatConsequences([], ["Operational overhead increases."])
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
