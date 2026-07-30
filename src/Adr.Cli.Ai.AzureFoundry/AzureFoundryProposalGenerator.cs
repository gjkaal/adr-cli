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
/// churn against this deployment. Existing-ADR summaries are stuffed directly into a single
/// request - no persistent agent, thread, or tool-calling round trip.
/// </summary>
public class AzureFoundryProposalGenerator : IAdrProposalGenerator
{
    /// <summary>
    /// Optional API key, read from the environment rather than adr.config.json so no secret ever
    /// needs to be committed. When unset, falls back to DefaultAzureCredential (az login locally,
    /// managed identity when hosted).
    /// </summary>
    private const string ApiKeyEnvironmentVariable = "ADR_CLI_AI_API_KEY";

    private const string SystemInstructions =
        "You draft the Context, Decision, and Consequences sections for a new Architecture Decision Record (ADR). " +
        "If the user already provided a Context, refine or complete it rather than discarding it - stay consistent with their intent. " +
        "If no Context was provided, draft one from the title and the existing ADRs listed in the prompt. " +
        "A template may be included as a structure and tone example - follow its style but never copy its placeholder text. " +
        "The Decision must follow directly from the Context you return. " +
        "Consequences are not prose - list them as separate pros and cons, each a short, distinct, beneficial or negative result " +
        "that follows directly from the Decision. A con may optionally end with a short mitigation in parentheses. " +
        "Never restate the Context or Decision as a pro or con.";

    private const string ProposalSchemaName = "adr_proposal";

    private static readonly BinaryData ProposalSchema = BinaryData.FromBytes(
        """
        {
            "type": "object",
            "properties": {
                "context": { "type": "string", "description": "The situation and forces behind the decision." },
                "decision": { "type": "string", "description": "The decision made. Must follow from context, not restate it." },
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
            "required": ["context", "decision", "pros", "cons"],
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
            var userPrompt = BuildUserPrompt(title, context, existingRecords, templateExample);
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(ProposalSchemaName, ProposalSchema, jsonSchemaIsStrict: true)
            };
            ChatCompletion completion = await chatClient.CompleteChatAsync(
                [new SystemChatMessage(SystemInstructions), new UserChatMessage(userPrompt)],
                options);

            var replyText = string.Concat(completion.Content.Select(part => part.Text));
            var proposal = ParseProposal(replyText);
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

    private static string BuildUserPrompt(string title, string context, IReadOnlyList<AdrSummary> existingRecords, string? templateExample)
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

        if (!string.IsNullOrWhiteSpace(templateExample))
        {
            prompt.AppendLine();
            prompt.AppendLine("This repository's ADR template, as a structure/tone example (do not copy its placeholder text):");
            prompt.AppendLine(templateExample);
        }

        if (existingRecords.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Existing ADRs in this repository, for consistency and to flag conflicts:");
            prompt.AppendLine(SearchExistingRecords([], existingRecords));
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Formats existing-ADR summaries, optionally filtered by keyword. Called with no keywords to
    /// inline the full grounding list into the prompt; kept as a standalone, directly-testable
    /// method rather than inlined into BuildUserPrompt.
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

    /// <summary>
    /// The model's raw structured-output shape - Consequences arrive as separate pros/cons lists so
    /// formatting into the Pro's/Con's markdown lists is under this application's control rather than
    /// the model's.
    /// </summary>
    private sealed class ProposalReply
    {
        public string Context { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public List<string> Pros { get; set; } = new();
        public List<string> Cons { get; set; } = new();
    }

    /// <summary>
    /// Parses the model's structured-output reply and formats pros/cons into the Consequences
    /// markdown. The chat request constrains the model to the <see cref="ProposalSchema" /> JSON
    /// shape, so this is a plain deserialization rather than markdown-section scanning - there is no
    /// partial/reordered-header case to recover from.
    /// </summary>
    internal static AdrProposal ParseProposal(string replyText)
    {
        ProposalReply? reply;
        try
        {
            reply = JsonSerializer.Deserialize<ProposalReply>(replyText, JsonOptions);
        }
        catch (JsonException)
        {
            reply = null;
        }

        if (reply == null)
        {
            return new AdrProposal();
        }

        return new AdrProposal
        {
            Context = reply.Context,
            Decision = reply.Decision,
            Consequences = FormatConsequences(reply.Pros, reply.Cons)
        };
    }

    /// <summary>
    /// Renders pros/cons as the two markdown lists ADRs in this repository expect under Consequences.
    /// </summary>
    private static string FormatConsequences(IReadOnlyList<string> pros, IReadOnlyList<string> cons)
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
    /// Structural check against the returned Context: catches the model producing an empty field, or
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
