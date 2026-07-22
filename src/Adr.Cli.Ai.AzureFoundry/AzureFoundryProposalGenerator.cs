using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Azure.AI.Agents.Persistent;
using Azure.Identity;

using McpCore;

using Microsoft.Extensions.Logging;

namespace Adr.Cli.Ai.AzureFoundry;

/// <summary>
/// Drafts ADR proposals with a persistent Azure AI Foundry agent. The agent is given a function
/// tool to search the existing-ADR summaries handed to <see cref="GenerateAsync" /> - it decides
/// what (if anything) is worth pulling into context, instead of the full list always being stuffed
/// into the prompt. Grounding stays local to this call: the tool searches the in-memory summaries
/// only, it never calls back into the ADR repository.
/// </summary>
public class AzureFoundryProposalGenerator : IAdrProposalGenerator
{
    private const string SearchToolName = "search_existing_adrs";
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IAdrSettings settings;
    private readonly ILogger<AzureFoundryProposalGenerator> logger;

    public AzureFoundryProposalGenerator(IAdrSettings settings, ILogger<AzureFoundryProposalGenerator> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    public async Task<Response<AdrProposal>> GenerateAsync(string title, string context, IReadOnlyList<AdrSummary> existingRecords)
    {
        try
        {
            var proposal = await GenerateWithAgentAsync(title, context, existingRecords);
            return new Response<AdrProposal>(true, "AI proposal generated.", proposal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI proposal generation failed for '{Title}'.", title);
            return new Response<AdrProposal>(false, $"AI proposal generation failed: {ex.Message}", new AdrProposal());
        }
    }

    private async Task<AdrProposal> GenerateWithAgentAsync(string title, string context, IReadOnlyList<AdrSummary> existingRecords)
    {
        var client = new PersistentAgentsClient(new Uri(settings.AiSettings.Endpoint).ToString(), new DefaultAzureCredential());

        var searchTool = new FunctionToolDefinition(
            name: SearchToolName,
            description: "Search the existing ADRs in this repository by keyword, to check for related or conflicting prior decisions.",
            parameters: BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    keywords = new { type = "string", description = "One or more space-separated keywords to search for in existing ADR titles and context." }
                },
                required = new[] { "keywords" }
            }));

        PersistentAgent agent = await client.Administration.CreateAgentAsync(
            model: settings.AiSettings.DeploymentName,
            name: "adr-cli-proposal-drafter",
            instructions:
                "You draft the Decision and Consequences sections for a new Architecture Decision Record (ADR). " +
                "Use the search_existing_adrs tool if you need to check for related or conflicting prior decisions. " +
                "Respond with exactly two markdown sections, in this order and with no other text: " +
                "'## Decision' followed by the decision text, then '## Consequences' followed by the consequences text.",
            tools: [searchTool]);

        try
        {
            PersistentAgentThread thread = await client.Threads.CreateThreadAsync();
            var userMessage = BuildUserMessage(title, context);
            await client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, userMessage);

            ThreadRun run = await client.Runs.CreateRunAsync(thread.Id, agent.Id);
            run = await PollUntilDoneAsync(client, thread.Id, run, existingRecords);

            if (run.Status != RunStatus.Completed)
            {
                throw new InvalidOperationException($"Agent run ended with status '{run.Status}'.");
            }

            var replyText = await GetLatestAssistantMessageAsync(client, thread.Id);
            return ParseProposal(replyText);
        }
        finally
        {
            await client.Administration.DeleteAgentAsync(agent.Id);
        }
    }

    private async Task<ThreadRun> PollUntilDoneAsync(PersistentAgentsClient client, string threadId, ThreadRun run, IReadOnlyList<AdrSummary> existingRecords)
    {
        var deadline = DateTime.UtcNow + RunTimeout;
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Agent run did not complete within {RunTimeout.TotalSeconds}s.");
            }

            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitToolOutputsAction)
            {
                var toolOutputs = submitToolOutputsAction.ToolCalls
                    .OfType<RequiredFunctionToolCall>()
                    .Select(toolCall => ExecuteTool(toolCall, existingRecords))
                    .ToList();

                run = await client.Runs.SubmitToolOutputsToRunAsync(threadId, run.Id, toolOutputs);
                continue;
            }

            await Task.Delay(PollInterval);
            run = await client.Runs.GetRunAsync(threadId, run.Id);
        }

        return run;
    }

    private static ToolOutput ExecuteTool(RequiredFunctionToolCall toolCall, IReadOnlyList<AdrSummary> existingRecords)
    {
        if (toolCall.Name != SearchToolName)
        {
            return new ToolOutput(toolCall.Id, string.Empty);
        }

        var keywords = Array.Empty<string>();
        try
        {
            using var arguments = JsonDocument.Parse(toolCall.Arguments);
            if (arguments.RootElement.TryGetProperty("keywords", out var keywordsElement))
            {
                keywords = (keywordsElement.GetString() ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
        }
        catch (JsonException)
        {
            // Malformed tool arguments - fall back to returning nothing rather than failing the run.
        }

        return new ToolOutput(toolCall.Id, SearchExistingRecords(keywords, existingRecords));
    }

    internal static string SearchExistingRecords(string[] keywords, IReadOnlyList<AdrSummary> existingRecords)
    {
        var matches = existingRecords
            .Where(record => keywords.Length == 0
                || keywords.Any(keyword =>
                    record.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || record.Context.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Select(record => $"{record.RecordId:D5} [{record.Status}] {record.Title} - {record.Context}")
            .ToList();

        return matches.Count == 0
            ? "No matching ADRs found."
            : string.Join('\n', matches);
    }

    private static async Task<string> GetLatestAssistantMessageAsync(PersistentAgentsClient client, string threadId)
    {
        await foreach (var message in client.Messages.GetMessagesAsync(threadId, order: ListSortOrder.Descending))
        {
            if (message.Role != MessageRole.Agent)
            {
                continue;
            }

            var text = new StringBuilder();
            foreach (var contentItem in message.ContentItems.OfType<MessageTextContent>())
            {
                text.Append(contentItem.Text);
            }

            return text.ToString();
        }

        throw new InvalidOperationException("The agent did not return a message.");
    }

    private static string BuildUserMessage(string title, string context)
    {
        var message = new StringBuilder();
        message.AppendLine($"Title: {title}");
        if (!string.IsNullOrWhiteSpace(context))
        {
            message.AppendLine($"Context: {context}");
        }

        return message.ToString();
    }

    internal static AdrProposal ParseProposal(string replyText)
    {
        var decisionIndex = replyText.IndexOf("## Decision", StringComparison.OrdinalIgnoreCase);
        var consequencesIndex = replyText.IndexOf("## Consequences", StringComparison.OrdinalIgnoreCase);

        if (decisionIndex < 0 || consequencesIndex < 0 || consequencesIndex < decisionIndex)
        {
            // The model didn't follow the requested format - return the whole reply as the
            // decision text rather than losing it, and leave consequences empty.
            return new AdrProposal { Decision = replyText.Trim() };
        }

        var decisionStart = decisionIndex + "## Decision".Length;
        var decision = replyText[decisionStart..consequencesIndex].Trim();

        var consequencesStart = consequencesIndex + "## Consequences".Length;
        var consequences = replyText[consequencesStart..].Trim();

        return new AdrProposal { Decision = decision, Consequences = consequences };
    }
}
