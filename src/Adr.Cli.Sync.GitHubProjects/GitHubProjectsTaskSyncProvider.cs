using Adr.Cli.CommandHandlers;
using Adr.Cli.Sync;

using McpCore;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// <see cref="ITaskSyncProvider" /> implementation for GitHub Projects (v2), supporting both
/// user-owned and organization-owned boards. Exports create/update a Draft Issue on the configured
/// project - never a repository-backed Issue, so export never requires a target repository or
/// repository permissions. Status flows through a single-select field on the board (named by
/// <see cref="GitHubProjectsSettings.StatusFieldName" />) via two independently-configured maps.
/// See ADR 00008 for the full design rationale.
/// </summary>
public class GitHubProjectsTaskSyncProvider : ITaskSyncProvider
{
    public const string ProviderName = "GitHubProjects";

    /// <summary>
    /// Used when "sync.patName" is not set in adr.config.json. Not GitHub-specific by name, since a
    /// repository only ever has one active provider at a time (see ADR 00008) - override via
    /// "sync.patName" when a machine works across multiple repositories/contexts that each need a
    /// distinct token under a distinct variable name.
    /// </summary>
    private const string DefaultPatEnvironmentVariable = "ADR_CLI_SYNC_PAT";

    private static readonly JsonSerializerOptions SettingsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IAdrSettings settings;
    private readonly ILogger<GitHubProjectsTaskSyncProvider> logger;
    private readonly IGitHubGraphQlClient? injectedClient;

    public GitHubProjectsTaskSyncProvider(IAdrSettings settings, ILogger<GitHubProjectsTaskSyncProvider> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    /// <summary>
    /// Test-only constructor allowing a fake <see cref="IGitHubGraphQlClient" /> to be injected
    /// instead of making real HTTP calls.
    /// </summary>
    internal GitHubProjectsTaskSyncProvider(IAdrSettings settings, ILogger<GitHubProjectsTaskSyncProvider> logger, IGitHubGraphQlClient client)
        : this(settings, logger)
    {
        injectedClient = client;
    }

    public string Name => ProviderName;

    public async Task<Response<TaskExportResult>> ExportAsync(TaskRecord task, SyncLink? existingLink, bool force = false, bool dryRun = false)
    {
        try
        {
            var (providerSettings, client) = Resolve();
            var project = await ResolveProjectAsync(client, providerSettings);

            var body = TaskBodyFormat.Combine(task.Description, task.Details);
            string itemId;
            bool created;
            var contentType = "DraftIssue";
            string? contentUrl = null;

            if (existingLink == null || string.IsNullOrWhiteSpace(existingLink.ExternalId))
            {
                if (dryRun)
                {
                    // Nothing exists to fetch or compare against yet - a brand-new export always
                    // proceeds (no baseline to check), so dry-run just reports the intended creation.
                    itemId = string.Empty;
                }
                else
                {
                    var newHash = ContentSyncMarker.ComputeHash(task.Title, body);
                    itemId = await CreateDraftIssueAsync(client, project.Id, task.Title, ContentSyncMarker.AppendMarker(body, newHash));
                }
                created = true;
            }
            else
            {
                itemId = existingLink.ExternalId;
                var (draftIssueId, remoteTitle, remoteBodyWithMarker, remoteContentType, remoteUrl) = await FetchItemContentAsync(client, itemId);
                contentType = remoteContentType;
                contentUrl = remoteUrl;

                if (draftIssueId == null)
                {
                    // A real repository issue/PR, not a draft issue this connector created - content
                    // can't be pushed without repository write permissions (see ADR 00008/00009), so
                    // only status is pushed below; local title/description stay as they are remotely.
                    logger.LogInformation("GitHub Projects export: item {ItemId} is a real issue/PR, not a draft issue - content push skipped, only status will be pushed.", itemId);
                }
                else if (!force && !ContentSyncMarker.IsRemoteSafeToOverwrite(remoteTitle, remoteBodyWithMarker))
                {
                    // Someone edited the item on GitHub since our last push - pushing now would
                    // silently discard their change. Refuse, touch nothing, let task-import surface
                    // this as a mismatch for manual resolution - unless the caller explicitly forced
                    // it, in which case local wins deliberately.
                    return new Response<TaskExportResult>(true, "Export refused: the GitHub item has changed since the last sync (content mismatch). Use --force to overwrite it anyway.", new TaskExportResult
                    {
                        Created = false,
                        ExternalScope = $"{providerSettings.Owner}/{providerSettings.ProjectNumber}",
                        ExternalId = itemId,
                        ExternalContentType = contentType,
                        SyncState = SyncState.Mismatch
                    });
                }
                else if (!dryRun)
                {
                    var newHash = ContentSyncMarker.ComputeHash(task.Title, body);
                    await UpdateDraftIssueContentAsync(client, draftIssueId, task.Title, ContentSyncMarker.AppendMarker(body, newHash));
                }

                created = false;
            }

            var syncState = SyncState.Unmapped;
            if (project.StatusFieldId != null
                && providerSettings.ExportStatusMap.TryGetValue(task.Status, out var optionName)
                && project.StatusOptionIdsByName.TryGetValue(optionName, out var optionId))
            {
                if (!dryRun)
                {
                    await SetStatusFieldAsync(client, project.Id, itemId, project.StatusFieldId, optionId);
                }
                syncState = SyncState.Synced;
            }
            else
            {
                logger.LogWarning("GitHub Projects export: no export status mapping found for local status {Status} - external status left unchanged.", task.Status);
            }

            var result = new TaskExportResult
            {
                Created = created,
                ExternalScope = $"{providerSettings.Owner}/{providerSettings.ProjectNumber}",
                ExternalId = itemId,
                ExternalContentType = contentType,
                ExternalUrl = contentUrl ?? (string.IsNullOrEmpty(itemId) ? null : $"{project.Url}?pane=issue&itemId={itemId}"),
                SyncState = syncState
            };
            var message = dryRun
                ? $"[DRY RUN] Would {(created ? "create" : "update")} the GitHub item{(syncState == SyncState.Synced ? " and set its status" : "")}. Nothing was changed."
                : null;
            return new Response<TaskExportResult>(true, message, result);
        }
        catch (Exception ex) when (ex is GitHubGraphQlException or InvalidOperationException)
        {
            return new Response<TaskExportResult>(false, ex.Message, new TaskExportResult());
        }
    }

    public async Task<Response<TaskImportResult>> ImportAsync(TaskRecord task, SyncLink existingLink, bool dryRun = false)
    {
        // Import itself is already read-only on the GitHub side; dryRun only affects whether the
        // caller persists locally or re-exports to refresh the marker after a safe pull.
        try
        {
            if (string.IsNullOrWhiteSpace(existingLink.ExternalId))
            {
                return new Response<TaskImportResult>(false, "Task has no GitHub Projects external identifier to import from.", new TaskImportResult());
            }

            var (providerSettings, client) = Resolve();
            var itemId = existingLink.ExternalId;

            var (_, remoteTitle, remoteBodyWithMarker, remoteContentType, remoteUrl) = await FetchItemContentAsync(client, itemId);
            var localBody = TaskBodyFormat.Combine(task.Description, task.Details);
            var contentOutcome = ContentSyncMarker.ResolveImport(task.Title, localBody, remoteTitle, remoteBodyWithMarker, out var pulledTitle, out var pulledBody);

            if (contentOutcome == ContentSyncOutcome.Mismatch)
            {
                return new Response<TaskImportResult>(true, "Import skipped: local content changed since the last sync while the GitHub item also diverged (content mismatch).", new TaskImportResult
                {
                    ExternalContentType = remoteContentType,
                    ExternalUrl = remoteUrl,
                    SyncState = SyncState.Mismatch
                });
            }

            var externalStatus = await ReadStatusFieldAsync(client, itemId, providerSettings.StatusFieldName);

            var result = new TaskImportResult
            {
                ExternalStatusRaw = externalStatus,
                ExternalContentType = remoteContentType,
                ExternalUrl = remoteUrl
            };
            if (contentOutcome == ContentSyncOutcome.Pull)
            {
                result.PulledTitle = pulledTitle;
                result.PulledBody = pulledBody;
            }

            if (externalStatus != null && providerSettings.ImportStatusMap.TryGetValue(externalStatus, out var mapped))
            {
                result.MappedStatus = mapped;
                result.SyncState = SyncState.Synced;
            }
            else
            {
                result.SyncState = SyncState.Unmapped;
                logger.LogWarning("GitHub Projects import: external status {ExternalStatus} has no import mapping - task left unchanged.", externalStatus ?? "(none)");
            }

            return new Response<TaskImportResult>(true, null, result);
        }
        catch (Exception ex) when (ex is GitHubGraphQlException or InvalidOperationException)
        {
            return new Response<TaskImportResult>(false, ex.Message, new TaskImportResult());
        }
    }

    public async Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync()
    {
        try
        {
            var (providerSettings, client) = Resolve();
            var project = await ResolveProjectAsync(client, providerSettings);

            const string query = """
                query($projectId: ID!) {
                  node(id: $projectId) {
                    ... on ProjectV2 {
                      items(first: 100) {
                        nodes {
                          id
                          content {
                            __typename
                            ... on DraftIssue { title body }
                            ... on Issue { title body }
                            ... on PullRequest { title body }
                          }
                        }
                      }
                    }
                  }
                }
                """;

            var data = await client.ExecuteAsync(query, new { projectId = project.Id });
            var scope = $"{providerSettings.Owner}/{providerSettings.ProjectNumber}";
            var items = new List<DiscoveredExternalItem>();

            foreach (var node in data.GetProperty("node").GetProperty("items").GetProperty("nodes").EnumerateArray())
            {
                // Every item on the board is a candidate regardless of whether its content is a
                // draft issue, a real repository issue, or a pull request - only items with no
                // readable content at all (shouldn't normally happen) are skipped.
                if (!node.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object
                    || !content.TryGetProperty("title", out var titleElement))
                {
                    continue;
                }

                var title = titleElement.GetString() ?? string.Empty;
                var bodyWithMarker = content.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
                var (remoteContent, hash) = ContentSyncMarker.SplitMarker(bodyWithMarker);

                items.Add(new DiscoveredExternalItem
                {
                    ExternalScope = scope,
                    ExternalId = node.GetProperty("id").GetString() ?? string.Empty,
                    Title = title,
                    Body = remoteContent,
                    HasMarker = hash != null
                });
            }

            return new Response<IReadOnlyList<DiscoveredExternalItem>>(true, null, items);
        }
        catch (Exception ex) when (ex is GitHubGraphQlException or InvalidOperationException)
        {
            return new Response<IReadOnlyList<DiscoveredExternalItem>>(false, ex.Message, []);
        }
    }

    private (GitHubProjectsSettings Settings, IGitHubGraphQlClient Client) Resolve()
    {
        var providerSettings = settings.SyncSettings.Settings.ValueKind == JsonValueKind.Undefined
            ? new GitHubProjectsSettings()
            : settings.SyncSettings.Settings.Deserialize<GitHubProjectsSettings>(SettingsJsonOptions) ?? new GitHubProjectsSettings();

        if (string.IsNullOrWhiteSpace(providerSettings.Owner) || providerSettings.ProjectNumber <= 0)
        {
            throw new InvalidOperationException(
                "GitHub Projects sync is not configured correctly. \"sync.settings\" needs \"ownerType\", \"owner\", and \"projectNumber\" in adr.config.json.");
        }

        if (injectedClient != null)
        {
            return (providerSettings, injectedClient);
        }

        var patEnvironmentVariable = string.IsNullOrWhiteSpace(settings.SyncSettings.SyncPatName)
            ? DefaultPatEnvironmentVariable
            : settings.SyncSettings.SyncPatName;

        var pat = Environment.GetEnvironmentVariable(patEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException(
                $"GitHub Projects sync requires a personal access token in the {patEnvironmentVariable} environment variable. " +
                $"Set \"sync.syncPatName\" in adr.config.json to read from a different variable, e.g. when this machine works across multiple repositories with different tokens.");
        }

        return (providerSettings, new GitHubGraphQlClient(new HttpClient(), pat));
    }

    private static async Task<ProjectInfo> ResolveProjectAsync(IGitHubGraphQlClient client, GitHubProjectsSettings providerSettings)
    {
        var ownerField = providerSettings.OwnerType == GitHubProjectOwnerType.Organization ? "organization" : "user";
        var query = $$"""
            query($login: String!, $number: Int!) {
              {{ownerField}}(login: $login) {
                projectV2(number: $number) {
                  id
                  url
                  fields(first: 50) {
                    nodes {
                      ... on ProjectV2SingleSelectField { id name options { id name } }
                    }
                  }
                }
              }
            }
            """;

        var data = await client.ExecuteAsync(query, new { login = providerSettings.Owner, number = providerSettings.ProjectNumber });
        if (!data.TryGetProperty(ownerField, out var ownerElement) || ownerElement.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException($"GitHub {ownerField} \"{providerSettings.Owner}\" was not found or is not accessible with the configured token.");
        }

        if (!ownerElement.TryGetProperty("projectV2", out var projectElement) || projectElement.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException($"GitHub Projects board #{providerSettings.ProjectNumber} was not found for \"{providerSettings.Owner}\".");
        }

        var projectId = projectElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("GitHub Projects response was missing the project id.");
        var projectUrl = projectElement.GetProperty("url").GetString() ?? string.Empty;

        string? statusFieldId = null;
        var optionIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in projectElement.GetProperty("fields").GetProperty("nodes").EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("name", out var nameElement))
            {
                continue;
            }

            if (!string.Equals(nameElement.GetString(), providerSettings.StatusFieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!field.TryGetProperty("options", out var optionsElement) || optionsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"GitHub Projects field \"{providerSettings.StatusFieldName}\" is not a single-select field, so it cannot be used for status mapping.");
            }

            statusFieldId = field.GetProperty("id").GetString();
            foreach (var option in optionsElement.EnumerateArray())
            {
                var optionName = option.GetProperty("name").GetString();
                var optionId = option.GetProperty("id").GetString();
                if (optionName != null && optionId != null)
                {
                    optionIdsByName[optionName] = optionId;
                }
            }

            break;
        }

        if (statusFieldId == null)
        {
            throw new InvalidOperationException(
                $"GitHub Projects board \"{providerSettings.Owner}/{providerSettings.ProjectNumber}\" has no single-select field named \"{providerSettings.StatusFieldName}\".");
        }

        return new ProjectInfo(projectId, projectUrl, statusFieldId, optionIdsByName);
    }

    private static async Task<string> CreateDraftIssueAsync(IGitHubGraphQlClient client, string projectId, string title, string body)
    {
        const string mutation = """
            mutation($projectId: ID!, $title: String!, $body: String!) {
              addProjectV2DraftIssue(input: { projectId: $projectId, title: $title, body: $body }) {
                projectItem { id }
              }
            }
            """;

        var data = await client.ExecuteAsync(mutation, new { projectId, title, body });
        return data.GetProperty("addProjectV2DraftIssue").GetProperty("projectItem").GetProperty("id").GetString()
            ?? throw new InvalidOperationException("GitHub Projects did not return an item id for the created draft issue.");
    }

    /// <summary>
    /// Fetches a project item's current title/body (body still carrying its trailing hash marker, if
    /// any), regardless of whether the underlying content is a draft issue or a real repository
    /// issue/pull request - status and content can be read/compared for any item on the board, not
    /// just ones this connector created. <see cref="DraftIssueId" /> is only non-null for draft
    /// issues, since <c>updateProjectV2DraftIssue</c> is the only content mutation available without
    /// repository write permissions - content pushed from this tool can only update draft issues;
    /// real issues/PRs are read-only from here.
    /// </summary>
    private static async Task<(string? DraftIssueId, string Title, string Body, string ContentType, string? Url)> FetchItemContentAsync(IGitHubGraphQlClient client, string itemId)
    {
        const string query = """
            query($itemId: ID!) {
              node(id: $itemId) {
                ... on ProjectV2Item {
                  content {
                    __typename
                    ... on DraftIssue { id title body }
                    ... on Issue { title body url }
                    ... on PullRequest { title body url }
                  }
                }
              }
            }
            """;

        var data = await client.ExecuteAsync(query, new { itemId });
        var content = data.GetProperty("node").GetProperty("content");
        if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("__typename", out var typeNameElement))
        {
            throw new InvalidOperationException($"GitHub Projects item {itemId} has no readable content (not a draft issue, issue, or pull request).");
        }

        var draftIssueId = content.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var title = content.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty;
        var body = content.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
        var contentType = typeNameElement.GetString() ?? string.Empty;
        var url = content.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        return (draftIssueId, title, body, contentType, url);
    }

    private static async Task UpdateDraftIssueContentAsync(IGitHubGraphQlClient client, string draftIssueId, string title, string body)
    {
        const string mutation = """
            mutation($draftIssueId: ID!, $title: String!, $body: String!) {
              updateProjectV2DraftIssue(input: { draftIssueId: $draftIssueId, title: $title, body: $body }) {
                draftIssue { id }
              }
            }
            """;

        await client.ExecuteAsync(mutation, new { draftIssueId, title, body });
    }

    private static async Task SetStatusFieldAsync(IGitHubGraphQlClient client, string projectId, string itemId, string fieldId, string optionId)
    {
        const string mutation = """
            mutation($projectId: ID!, $itemId: ID!, $fieldId: ID!, $optionId: String!) {
              updateProjectV2ItemFieldValue(
                input: { projectId: $projectId, itemId: $itemId, fieldId: $fieldId, value: { singleSelectOptionId: $optionId } }
              ) {
                projectV2Item { id }
              }
            }
            """;

        await client.ExecuteAsync(mutation, new { projectId, itemId, fieldId, optionId });
    }

    private static async Task<string?> ReadStatusFieldAsync(IGitHubGraphQlClient client, string itemId, string fieldName)
    {
        const string query = """
            query($itemId: ID!, $fieldName: String!) {
              node(id: $itemId) {
                ... on ProjectV2Item {
                  fieldValueByName(name: $fieldName) {
                    ... on ProjectV2ItemFieldSingleSelectValue { name }
                  }
                }
              }
            }
            """;

        var data = await client.ExecuteAsync(query, new { itemId, fieldName });
        var node = data.GetProperty("node");
        if (node.ValueKind == JsonValueKind.Null || !node.TryGetProperty("fieldValueByName", out var fieldValue) || fieldValue.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return fieldValue.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
    }

    private sealed record ProjectInfo(string Id, string Url, string? StatusFieldId, Dictionary<string, string> StatusOptionIdsByName);
}
