using System.Collections.Generic;

using Adr.Cli.CommandHandlers;

using Xunit;

namespace Adr.Cli.Ai.AzureFoundry;

public sealed class WithAzureFoundryTaskProposalGenerator
{
    [Fact]
    public void ParseProposal_WellFormedReply_SplitsDescriptionAndDetails()
    {
        var reply =
            "## Description\n" +
            "Wire up the CI pipeline for the new service.\n\n" +
            "## Details\n" +
            "Add build, test, and lint stages triggered on pull request.";

        var proposal = AzureFoundryTaskProposalGenerator.ParseProposal(reply);

        Assert.Equal("Wire up the CI pipeline for the new service.", proposal.Description);
        Assert.Equal("Add build, test, and lint stages triggered on pull request.", proposal.Details);
    }

    [Fact]
    public void ParseProposal_MissingMarkers_FallsBackToWholeReplyAsDescription()
    {
        var reply = "I recommend wiring up a CI pipeline.";

        var proposal = AzureFoundryTaskProposalGenerator.ParseProposal(reply);

        Assert.Equal(reply, proposal.Description);
        Assert.Equal(string.Empty, proposal.Details);
    }

    [Fact]
    public void ParseProposal_DetailsBeforeDescription_FallsBackToWholeReplyAsDescription()
    {
        var reply = "## Details\nSome text\n## Description\nSome other text";

        var proposal = AzureFoundryTaskProposalGenerator.ParseProposal(reply);

        Assert.Equal(reply, proposal.Description);
        Assert.Equal(string.Empty, proposal.Details);
    }

    [Fact]
    public void SearchExistingTasks_NoKeywords_ReturnsAllTasks()
    {
        var tasks = new List<TaskSummary>
        {
            new() { RecordId = 1, Title = "Set up CI pipeline", Status = PlanningStatus.New, Description = "Automate build and test" },
            new() { RecordId = 2, Title = "Write onboarding docs", Status = PlanningStatus.New, Description = "Help new contributors" }
        };

        var result = AzureFoundryTaskProposalGenerator.SearchExistingTasks([], tasks);

        Assert.Contains("Set up CI pipeline", result);
        Assert.Contains("Write onboarding docs", result);
    }

    [Fact]
    public void SearchExistingTasks_MatchingKeyword_ReturnsOnlyMatchingTasks()
    {
        var tasks = new List<TaskSummary>
        {
            new() { RecordId = 1, Title = "Set up CI pipeline", Status = PlanningStatus.New, Description = "Automate build and test" },
            new() { RecordId = 2, Title = "Write onboarding docs", Status = PlanningStatus.New, Description = "Help new contributors" }
        };

        var result = AzureFoundryTaskProposalGenerator.SearchExistingTasks(["pipeline"], tasks);

        Assert.Contains("Set up CI pipeline", result);
        Assert.DoesNotContain("Write onboarding docs", result);
    }

    [Fact]
    public void SearchExistingTasks_NoMatches_ReturnsNotFoundMessage()
    {
        var tasks = new List<TaskSummary>
        {
            new() { RecordId = 1, Title = "Set up CI pipeline", Status = PlanningStatus.New, Description = "Automate build and test" }
        };

        var result = AzureFoundryTaskProposalGenerator.SearchExistingTasks(["kubernetes"], tasks);

        Assert.Equal("No matching tasks found.", result);
    }
}
