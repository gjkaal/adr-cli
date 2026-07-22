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
/// Drafts task proposals via the Azure OpenAI Chat Completions API. Parallel to
/// <see cref="AzureFoundryProposalGenerator" /> rather than sharing an abstraction with it - a task
/// describes a unit of work to be done, not a decision and its consequences, so its prompt and
/// parsing are kept independent (see ADR 00007).
/// </summary>
public class AzureFoundryTaskProposalGenerator : ITaskProposalGenerator
{
    /// <summary>
    /// Optional API key, read from the environment rather than adr.config.json so no secret ever
    /// needs to be committed. When unset, falls back to DefaultAzureCredential (az login locally,
    /// managed identity when hosted).
    /// </summary>
    private const string ApiKeyEnvironmentVariable = "ADR_CLI_AI_API_KEY";

    private const string SystemInstructions =
        "You draft the Description and Details sections for a new project planning task. A task " +
        "describes a concrete unit of work to be done - not an architectural decision or its " +
        "consequences. If the user already provided a Description, refine or complete it rather than " +
        "discarding it - stay consistent with their intent. If no Description was provided, draft one " +
        "from the title and the existing tasks listed in the prompt. A template may be included as a " +
        "structure and tone example - follow its style but never copy its placeholder text. " +
        "Respond with exactly two markdown sections, in this order and with no other text: " +
        "'## Description' followed by the description text, then '## Details' followed by the details text.";

    private readonly IAdrSettings settings;
    private readonly ILogger<AzureFoundryTaskProposalGenerator> logger;

    public AzureFoundryTaskProposalGenerator(IAdrSettings settings, ILogger<AzureFoundryTaskProposalGenerator> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    public async Task<Response<TaskProposal>> GenerateAsync(string title, string description, IReadOnlyList<TaskSummary> existingTasks, string templateType)
    {
        try
        {
            var azureClient = CreateClient();
            var chatClient = azureClient.GetChatClient(settings.AiSettings.DeploymentName);

            var templateExample = await TryReadTemplateExampleAsync(templateType);
            var userPrompt = BuildUserPrompt(title, description, existingTasks, templateExample);
            ChatCompletion completion = await chatClient.CompleteChatAsync(
                new SystemChatMessage(SystemInstructions),
                new UserChatMessage(userPrompt));

            var replyText = string.Concat(completion.Content.Select(part => part.Text));
            var proposal = ParseProposal(replyText);
            return new Response<TaskProposal>(true, "AI proposal generated.", proposal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI proposal generation failed for '{Title}'.", title);
            return new Response<TaskProposal>(false, $"AI proposal generation failed: {ex.Message}", new TaskProposal());
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

    private static string BuildUserPrompt(string title, string description, IReadOnlyList<TaskSummary> existingTasks, string? templateExample)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Title: {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            prompt.AppendLine($"Description so far (refine or complete it, don't discard it): {description}");
        }
        else
        {
            prompt.AppendLine("No description has been written yet - draft one from the title and the existing tasks below.");
        }

        if (!string.IsNullOrWhiteSpace(templateExample))
        {
            prompt.AppendLine();
            prompt.AppendLine("This repository's task template, as a structure/tone example (do not copy its placeholder text):");
            prompt.AppendLine(templateExample);
        }

        if (existingTasks.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Existing tasks in this repository, for consistency and to flag related/duplicate work:");
            prompt.AppendLine(SearchExistingTasks([], existingTasks));
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Formats existing-task summaries, optionally filtered by keyword. Called with no keywords to
    /// inline the full grounding list into the prompt; kept as a standalone, directly-testable
    /// method rather than inlined into BuildUserPrompt.
    /// </summary>
    internal static string SearchExistingTasks(string[] keywords, IReadOnlyList<TaskSummary> existingTasks)
    {
        var matches = existingTasks
            .Where(task => keywords.Length == 0
                || keywords.Any(keyword =>
                    task.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || task.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Select(task => $"{task.RecordId:D5} [{task.Status}] {task.Title} - {task.Description}")
            .ToList();

        return matches.Count == 0
            ? "No matching tasks found."
            : string.Join('\n', matches);
    }

    internal static TaskProposal ParseProposal(string replyText)
    {
        var descriptionIndex = replyText.IndexOf("## Description", StringComparison.OrdinalIgnoreCase);
        var detailsIndex = replyText.IndexOf("## Details", StringComparison.OrdinalIgnoreCase);

        if (descriptionIndex >= 0 && detailsIndex > descriptionIndex)
        {
            return new TaskProposal
            {
                Description = replyText[(descriptionIndex + "## Description".Length)..detailsIndex].Trim(),
                Details = replyText[(detailsIndex + "## Details".Length)..].Trim()
            };
        }

        // The model didn't follow the requested format - return the whole reply as the
        // description text rather than losing it, and leave details empty.
        return new TaskProposal { Description = replyText.Trim() };
    }
}
