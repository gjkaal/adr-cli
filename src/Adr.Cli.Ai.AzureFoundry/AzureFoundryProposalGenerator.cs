using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        "Respond with exactly three markdown sections, in this order and with no other text: " +
        "'## Context' followed by the context text, then '## Decision' followed by the decision text, then '## Consequences' followed by the consequences text.";

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
            ChatCompletion completion = await chatClient.CompleteChatAsync(
                new SystemChatMessage(SystemInstructions),
                new UserChatMessage(userPrompt));

            var replyText = string.Concat(completion.Content.Select(part => part.Text));
            var proposal = ParseProposal(replyText);
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

    internal static AdrProposal ParseProposal(string replyText)
    {
        var contextIndex = replyText.IndexOf("## Context", StringComparison.OrdinalIgnoreCase);
        var decisionIndex = replyText.IndexOf("## Decision", StringComparison.OrdinalIgnoreCase);
        var consequencesIndex = replyText.IndexOf("## Consequences", StringComparison.OrdinalIgnoreCase);

        if (contextIndex >= 0 && decisionIndex > contextIndex && consequencesIndex > decisionIndex)
        {
            return new AdrProposal
            {
                Context = replyText[(contextIndex + "## Context".Length)..decisionIndex].Trim(),
                Decision = replyText[(decisionIndex + "## Decision".Length)..consequencesIndex].Trim(),
                Consequences = replyText[(consequencesIndex + "## Consequences".Length)..].Trim()
            };
        }

        if (decisionIndex >= 0 && consequencesIndex > decisionIndex)
        {
            // Model replied with just the older two-section format - still usable, Context is left empty.
            return new AdrProposal
            {
                Decision = replyText[(decisionIndex + "## Decision".Length)..consequencesIndex].Trim(),
                Consequences = replyText[(consequencesIndex + "## Consequences".Length)..].Trim()
            };
        }

        // The model didn't follow the requested format - return the whole reply as the
        // decision text rather than losing it, and leave context/consequences empty.
        return new AdrProposal { Decision = replyText.Trim() };
    }
}
