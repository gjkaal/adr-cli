using System.Collections.Generic;

using Xunit;

namespace Adr.Cli.Ai.AzureFoundry;

public sealed class WithAzureFoundryProposalGenerator
{
    [Fact]
    public void ParseProposal_WellFormedThreeSectionReply_SplitsContextDecisionAndConsequences()
    {
        var reply =
            "## Context\n" +
            "Services currently call each other synchronously over HTTP.\n\n" +
            "## Decision\n" +
            "Adopt a message bus for service integration.\n\n" +
            "## Consequences\n" +
            "Operational overhead increases, coupling decreases.";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal("Services currently call each other synchronously over HTTP.", proposal.Context);
        Assert.Equal("Adopt a message bus for service integration.", proposal.Decision);
        Assert.Equal("Operational overhead increases, coupling decreases.", proposal.Consequences);
    }

    [Fact]
    public void ParseProposal_TwoSectionReplyWithNoContextMarker_SplitsDecisionAndConsequencesWithEmptyContext()
    {
        var reply =
            "## Decision\n" +
            "Adopt a message bus for service integration.\n\n" +
            "## Consequences\n" +
            "Operational overhead increases, coupling decreases.";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal(string.Empty, proposal.Context);
        Assert.Equal("Adopt a message bus for service integration.", proposal.Decision);
        Assert.Equal("Operational overhead increases, coupling decreases.", proposal.Consequences);
    }

    [Fact]
    public void ParseProposal_MissingMarkers_FallsBackToWholeReplyAsDecision()
    {
        var reply = "I recommend adopting a message bus.";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal(reply, proposal.Decision);
        Assert.Equal(string.Empty, proposal.Consequences);
    }

    [Fact]
    public void ParseProposal_ConsequencesBeforeDecision_FallsBackToWholeReplyAsDecision()
    {
        var reply = "## Consequences\nSome text\n## Decision\nSome other text";

        var proposal = AzureFoundryProposalGenerator.ParseProposal(reply);

        Assert.Equal(reply, proposal.Decision);
        Assert.Equal(string.Empty, proposal.Consequences);
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
