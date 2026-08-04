using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Azure.AI.OpenAI;
using Azure.Identity;

using McpCore;

using Microsoft.Extensions.Logging;

using OpenAI.Chat;

namespace Adr.Cli.Ai.AzureFoundry;

/// <summary>
/// Drafts ADR proposals via the Azure OpenAI Chat Completions API - the stable, GA surface, not the
/// newer/experimental Responses or Projects/Agents APIs, both of which had unresolvable version
/// churn against this deployment. Context, Decision, and Consequences are drafted as three
/// sequential requests rather than one combined structured-output call - a single call asking the
/// model for all three fields at once was found to intermittently return an empty Decision and/or
/// Consequences even with a strict JSON schema. Each call has a narrower job and a smaller schema,
/// and later calls are grounded in the earlier calls' actual output (not just the user's input).
/// </summary>
public class AzureFoundryProposalGenerator : IAdrProposalGenerator
{
    /// <summary>
    /// Optional API key, read from the environment rather than adr.config.json so no secret ever
    /// needs to be committed. When unset, falls back to DefaultAzureCredential (az login locally,
    /// managed identity when hosted).
    /// </summary>
    private const string ApiKeyEnvironmentVariable = "ADR_CLI_AI_API_KEY";

    private const string ContextSystemInstructions =
        "You draft the Context section for a new Architecture Decision Record (ADR) - the situation and " +
        "forces behind the decision, not the decision itself. If the user already provided a Context, " +
        "refine or complete it rather than discarding it - stay consistent with their intent. If no " +
        "Context was provided, draft one from the title and the existing ADRs listed in the prompt. A " +
        "template may be included as a structure and tone example - follow its style but never copy its " +
        "placeholder text.";

    private const string DecisionSystemInstructions =
        "You draft the Decision section for a new Architecture Decision Record (ADR), given its Title and " +
        "Context. The Decision must follow directly from the Context - state what was decided and, " +
        "briefly, why. Never restate the Context. A template may be included as a structure and tone " +
        "example - follow its style but never copy its placeholder text.";

    private const string ConsequencesSystemInstructions =
        "You draft the Consequences section for a new Architecture Decision Record (ADR), given its " +
        "Title, Context, and Decision. Consequences are not prose - list them as separate pros and cons, " +
        "each a short, distinct, beneficial or negative result that follows directly from the Decision. " +
        "A con may optionally end with a short mitigation in parentheses. Never restate the Context or " +
        "Decision as a pro or con.";

    private static readonly BinaryData ContextSchema = BinaryData.FromBytes(
        """
        {
            "type": "object",
            "properties": {
                "context": { "type": "string", "description": "The situation and forces behind the decision." }
            },
            "required": ["context"],
            "additionalProperties": false
        }
        """u8.ToArray());

    private static readonly BinaryData DecisionSchema = BinaryData.FromBytes(
        """
        {
            "type": "object",
            "properties": {
                "decision": { "type": "string", "description": "The decision made. Must follow from context, not restate it." }
            },
            "required": ["decision"],
            "additionalProperties": false
        }
        """u8.ToArray());

    private static readonly BinaryData ConsequencesSchema = BinaryData.FromBytes(
        """
        {
            "type": "object",
            "properties": {
                "pros": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Beneficial results of the decision, one per entry."
                },
                "cons": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Negative results of the decision, one per entry, optionally ending with a mitigation in parentheses."
                }
            },
            "required": ["pros", "cons"],
            "additionalProperties": false
        }
        """u8.ToArray());

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IAdrSettings settings;
    private readonly ILogger<AzureFoundryProposalGenerator> logger;

    public AzureFoundryProposalGenerator(IAdrSettings settings, ILogger<AzureFoundryProposalGenerator> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    public async Task<Response<AdrProposal>> GenerateAsync(string title, string context, IReadOnlyList<AdrSummary> existingRecords, string templateType)
    {
        try
        {
            var azureClient = CreateClient();
            var chatClient = azureClient.GetChatClient(settings.AiSettings.DeploymentName);
            var templateExample = await TryReadTemplateExampleAsync(templateType);

            var draftedContext = await DraftContextAsync(chatClient, title, context, existingRecords, templateExample);
            var draftedDecision = await DraftDecisionAsync(chatClient, title, draftedContext, templateExample);
            var (pros, cons) = await DraftConsequencesAsync(chatClient, title, draftedContext, draftedDecision);

            var proposal = new AdrProposal
            {
                Context = draftedContext,
                Decision = draftedDecision,
                Consequences = FormatConsequences(pros, cons)
            };

            if (!IsWellFormed(proposal))
            {
                logger.LogWarning("AI proposal generation returned incomplete or degenerate content for '{Title}'.", title);
                return new Response<AdrProposal>(
                    false,
                    "AI proposal generation returned incomplete or degenerate content - Context, Decision, and Consequences must each be non-empty and distinct from one another.",
                    new AdrProposal());
            }

            return new Response<AdrProposal>(true, "AI proposal generated.", proposal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI proposal generation failed for '{Title}'.", title);
            return new Response<AdrProposal>(false, $"AI proposal generation failed: {ex.Message}", new AdrProposal());
        }
    }

    private async Task<string> DraftContextAsync(ChatClient chatClient, string title, string context, IReadOnlyList<AdrSummary> existingRecords, string? templateExample)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Title: {title}");
        if (!string.IsNullOrWhiteSpace(context))
        {
            prompt.AppendLine($"Context so far (refine or complete it, don't discard it): {context}");
        }
        else
        {
            prompt.AppendLine("No context has been written yet - draft one from the title and the existing ADRs below.");
        }

        AppendTemplateExample(prompt, templateExample);

        if (existingRecords.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Existing ADRs in this repository, for consistency and to flag conflicts:");
            prompt.AppendLine(SearchExistingRecords([], existingRecords));
        }

        var replyText = await CompleteAsync(chatClient, ContextSystemInstructions, prompt.ToString(), "adr_context", ContextSchema);
        return ParseContext(replyText);
    }

    private async Task<string> DraftDecisionAsync(ChatClient chatClient, string title, string draftedContext, string? templateExample)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Title: {title}");
        prompt.AppendLine($"Context: {draftedContext}");
        AppendTemplateExample(prompt, templateExample);

        var replyText = await CompleteAsync(chatClient, DecisionSystemInstructions, prompt.ToString(), "adr_decision", DecisionSchema);
        return ParseDecision(replyText);
    }

    private async Task<(List<string> Pros, List<string> Cons)> DraftConsequencesAsync(ChatClient chatClient, string title, string draftedContext, string draftedDecision)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Title: {title}");
        prompt.AppendLine($"Context: {draftedContext}");
        prompt.AppendLine($"Decision: {draftedDecision}");

        var replyText = await CompleteAsync(chatClient, ConsequencesSystemInstructions, prompt.ToString(), "adr_consequences", ConsequencesSchema);
        return ParseConsequences(replyText);
    }

    private static async Task<string> CompleteAsync(ChatClient chatClient, string systemInstructions, string userPrompt, string schemaName, BinaryData schema)
    {
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(schemaName, schema, jsonSchemaIsStrict: true)
        };
        ChatCompletion completion = await chatClient.CompleteChatAsync(
            [new SystemChatMessage(systemInstructions), new UserChatMessage(userPrompt)],
            options);

        return string.Concat(completion.Content.Select(part => part.Text));
    }

    private static void AppendTemplateExample(StringBuilder prompt, string? templateExample)
    {
        if (string.IsNullOrWhiteSpace(templateExample))
        {
            return;
        }

        prompt.AppendLine();
        prompt.AppendLine("This repository's ADR template, as a structure/tone example (do not copy its placeholder text):");
        prompt.AppendLine(templateExample);
    }

    private AzureOpenAIClient CreateClient()
    {
        var endpoint = new Uri(settings.AiSettings.Endpoint);
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

        return string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey));
    }

    /// <summary>
    /// Best-effort lookup of the on-disk template for <paramref name="templateType" />, used to show
    /// the model a real structure/tone example. Returns null rather than creating the template file
    /// when it doesn't exist yet - AI drafting must never have the side effect of writing files.
    /// </summary>
    private async Task<string?> TryReadTemplateExampleAsync(string templateType)
    {
        if (string.IsNullOrEmpty(templateType))
        {
            return null;
        }

        try
        {
            var templateFile = settings.GetTemplate(templateType);
            if (!templateFile.Exists)
            {
                return null;
            }

            using var reader = templateFile.OpenText();
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            // Best-effort only - the template is a style example, never a prerequisite for drafting.
            logger.LogDebug(ex, "Could not read template '{TemplateType}' for AI prompt grounding.", templateType);
            return null;
        }
    }

    /// <summary>
    /// Formats existing-ADR summaries, optionally filtered by keyword. Called with no keywords to
    /// inline the full grounding list into the prompt; kept as a standalone, directly-testable
    /// method rather than inlined into the prompt builders.
    /// </summary>
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

    private sealed class ContextReply
    {
        public string Context { get; set; } = string.Empty;
    }

    private sealed class DecisionReply
    {
        public string Decision { get; set; } = string.Empty;
    }

    /// <summary>
    /// The model's raw structured-output shape for the Consequences call - pros/cons arrive as
    /// separate lists so formatting into the Pro's/Con's markdown lists is under this application's
    /// control rather than the model's.
    /// </summary>
    private sealed class ConsequencesReply
    {
        public List<string> Pros { get; set; } = new();
        public List<string> Cons { get; set; } = new();
    }

    internal static string ParseContext(string replyText)
    {
        return TryDeserialize<ContextReply>(replyText)?.Context ?? string.Empty;
    }

    internal static string ParseDecision(string replyText)
    {
        return TryDeserialize<DecisionReply>(replyText)?.Decision ?? string.Empty;
    }

    internal static (List<string> Pros, List<string> Cons) ParseConsequences(string replyText)
    {
        var reply = TryDeserialize<ConsequencesReply>(replyText);
        return reply == null ? (new List<string>(), new List<string>()) : (reply.Pros, reply.Cons);
    }

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Renders pros/cons as the two markdown lists ADRs in this repository expect under Consequences.
    /// </summary>
    internal static string FormatConsequences(IReadOnlyList<string> pros, IReadOnlyList<string> cons)
    {
        if (pros.Count == 0 && cons.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("*Pro's:*\n");
        foreach (var pro in pros)
        {
            sb.Append("- ").Append(pro.Trim()).Append('\n');
        }

        sb.Append('\n');
        sb.Append("*Con's:*\n");
        foreach (var con in cons)
        {
            sb.Append("- ").Append(con.Trim()).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Structural check against the returned Context: catches a call producing an empty field, or
    /// degenerate output where Decision restates Context, or Consequences missing a pro or a con
    /// entirely. Purely local - no extra AI round trip.
    /// </summary>
    internal static bool IsWellFormed(AdrProposal proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal.Context)
            || string.IsNullOrWhiteSpace(proposal.Decision)
            || string.IsNullOrWhiteSpace(proposal.Consequences))
        {
            return false;
        }

        if (!proposal.Consequences.Contains("Pro's:", StringComparison.OrdinalIgnoreCase)
            || !proposal.Consequences.Contains("Con's:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !proposal.Decision.Equals(proposal.Context, StringComparison.OrdinalIgnoreCase);
    }
}
