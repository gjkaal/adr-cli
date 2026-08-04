using Adr.Cli.CommandHandlers;
using Adr.Cli.Sync;

using McpCore;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// <see cref="IAdrSyncProvider" /> implementation for GitHub Projects (v2). Unlike
/// <see cref="GitHubProjectsTaskSyncProvider" />, exports always create/update a real
/// repository-backed Issue (never a Draft Issue) in <see cref="GitHubProjectsSettings.TargetRepository" />,
/// since GitHub's sub-issue relationship only links Issue-to-Issue. Status flows through a dedicated
/// single-select field (<see cref="GitHubProjectsSettings.AdrStatusFieldName" />), separate from the
/// task board's status field. See ADR 00010 for the full design rationale.
/// </summary>
public class GitHubProjectsAdrSyncProvider : IAdrSyncProvider
{
    public const string ProviderName = "GitHubProjects";

    private const string DefaultPatEnvironmentVariable = "ADR_CLI_SYNC_PAT";

    /// <summary>
    /// Seeded options for an auto-created ADR status field - see <see cref="CreateAdrStatusFieldAsync" />.
    /// Names match <see cref="AdrStatus" />'s enum names verbatim.
    /// </summary>
    private static readonly (string Name, string Color)[] DefaultAdrStatusOptions =
    [
        ("New", "GRAY"),
        ("Proposed", "BLUE"),
        ("Final", "YELLOW"),
        ("Accepted", "GREEN"),
        ("Error", "RED"),
        ("Obsolete", "PURPLE")
    ];

    private static readonly JsonSerializerOptions SettingsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IAdrSettings settings;
    private readonly ILogger<GitHubProjectsAdrSyncProvider> logger;
    private readonly IGitHubGraphQlClient? injectedClient;

    public GitHubProjectsAdrSyncProvider(IAdrSettings settings, ILogger<GitHubProjectsAdrSyncProvider> logger)
    {
        this.settings = settings;
        this.logger = logger;
    }

    /// <summary>
    /// Test-only constructor allowing a fake <see cref="IGitHubGraphQlClient" /> to be injected
    /// instead of making real HTTP calls.
    /// </summary>
    internal GitHubProjectsAdrSyncProvider(IAdrSettings settings, ILogger<GitHubProjectsAdrSyncProvider> logger, IGitHubGraphQlClient client)
        : this(settings, logger)
    {
        injectedClient = client;
    }

    public string Name => ProviderName;

    public async Task<Response<AdrExportResult>> ExportAsync(AdrRecord adr, SyncLink? existingLink, bool force = false, bool dryRun = false)
    {
        try
        {
            var (providerSettings, client) = Resolve();
            var project = await ResolveProjectAsync(client, providerSettings, allowCreate: !dryRun);

            var body = AdrBodyFormat.Combine(adr.Context, adr.Decision, adr.Consequences);
            string itemId;
            string issueNodeId;
            bool created;
            string? contentUrl = null;

            if (existingLink == null || string.IsNullOrWhiteSpace(existingLink.ExternalId))
            {
                if (dryRun)
                {
                    itemId = string.Empty;
                    issueNodeId = string.Empty;
                }
                else
                {
                    var repositoryId = await ResolveRepositoryIdAsync(client, providerSettings.TargetRepository);
                    var newHash = ContentSyncMarker.ComputeHash(adr.Title, body);
                    (issueNodeId, contentUrl) = await CreateIssueAsync(client, repositoryId, adr.Title, ContentSyncMarker.AppendMarker(body, newHash));
                    itemId = await AddProjectItemAsync(client, project.Id, issueNodeId);
                }
                created = true;
            }
            else
            {
                itemId = existingLink.ExternalId;
                var (contentNodeId, remoteTitle, remoteBodyWithMarker, remoteContentType, remoteUrl) = await FetchItemContentAsync(client, itemId);
                issueNodeId = contentNodeId;
                contentUrl = remoteUrl;

                if (!force && !ContentSyncMarker.IsRemoteSafeToOverwrite(remoteTitle, remoteBodyWithMarker))
                {
                    return new Response<AdrExportResult>(true, "Export refused: the GitHub issue has changed since the last sync (content mismatch). Use --force to overwrite it anyway.", new AdrExportResult
                    {
                        Created = false,
                        ExternalScope = $"{providerSettings.Owner}/{providerSettings.ProjectNumber}",
                        ExternalId = itemId,
                        ExternalContentType = remoteContentType,
                        IssueNodeId = issueNodeId,
                        SyncState = SyncState.Mismatch
                    });
                }

                if (!dryRun)
                {
                    var newHash = ContentSyncMarker.ComputeHash(adr.Title, body);
                    await UpdateIssueContentAsync(client, issueNodeId, adr.Title, ContentSyncMarker.AppendMarker(body, newHash));
                }

                created = false;
            }

            var syncState = SyncState.Unmapped;
            if (project.AdrStatusFieldId != null
                && providerSettings.AdrExportStatusMap.TryGetValue(adr.Status, out var optionName)
                && project.AdrStatusOptionIdsByName.TryGetValue(optionName, out var optionId))
            {
                if (!dryRun)
                {
                    await SetStatusFieldAsync(client, project.Id, itemId, project.AdrStatusFieldId, optionId);
                }
                syncState = SyncState.Synced;
            }
            else
            {
                logger.LogWarning("GitHub Projects ADR export: no export status mapping found for local status {Status} - external status left unchanged.", adr.Status);
            }

            var result = new AdrExportResult
            {
                Created = created,
                ExternalScope = $"{providerSettings.Owner}/{providerSettings.ProjectNumber}",
                ExternalId = itemId,
                ExternalContentType = "Issue",
                ExternalUrl = contentUrl,
                IssueNodeId = issueNodeId,
                SyncState = syncState
            };
            var message = dryRun
                ? $"[DRY RUN] Would {(created ? "create" : "update")} the GitHub issue{(syncState == SyncState.Synced ? " and set its status" : "")}. Nothing was changed."
                : null;
            return new Response<AdrExportResult>(true, message, result);
        }
        catch (Exception ex) when (ex is GitHubGraphQlException or InvalidOperationException)
        {
            return new Response<AdrExportResult>(false, ex.Message, new AdrExportResult());
        }
    }

    public async Task<Response<AdrImportResult>> ImportAsync(AdrRecord adr, SyncLink existingLink, bool dryRun = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(existingLink.ExternalId))
            {
                return new Response<AdrImportResult>(false, "ADR has no GitHub Projects external identifier to import from.", new AdrImportResult());
            }

            var (providerSettings, client) = Resolve();
            var itemId = existingLink.ExternalId;

            var (_, remoteTitle, remoteBodyWithMarker, remoteContentType, remoteUrl) = await FetchItemContentAsync(client, itemId);
            var localBody = AdrBodyFormat.Combine(adr.Context, adr.Decision, adr.Consequences);
            var contentOutcome = ContentSyncMarker.ResolveImport(adr.Title, localBody, remoteTitle, remoteBodyWithMarker, out var pulledTitle, out var pulledBody);

            if (contentOutcome == ContentSyncOutcome.Mismatch)
            {
                return new Response<AdrImportResult>(true, "Import skipped: local content changed since the last sync while the GitHub issue also diverged (content mismatch).", new AdrImportResult
                {
                    ExternalContentType = remoteContentType,
                    ExternalUrl = remoteUrl,
                    SyncState = SyncState.Mismatch
                });
            }

            var externalStatus = await ReadStatusFieldAsync(client, itemId, providerSettings.AdrStatusFieldName);

            var result = new AdrImportResult
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

            if (externalStatus != null && providerSettings.AdrImportStatusMap.TryGetValue(externalStatus, out var mapped))
            {
                result.MappedStatus = mapped;
                result.SyncState = SyncState.Synced;
            }
            else
            {
                result.SyncState = SyncState.Unmapped;
                logger.LogWarning("GitHub Projects ADR import: external status {ExternalStatus} has no import mapping - ADR left unchanged.", externalStatus ?? "(none)");
            }

            return new Response<AdrImportResult>(true, null, result);
        }
        catch (Exception ex) when (ex is GitHubGraphQlException or InvalidOperationException)
        {
            return new Response<AdrImportResult>(false, ex.Message, new AdrImportResult());
        }
    }

    public async Task<Response<IReadOnlyList<DiscoveredExternalItem>>> DiscoverItemsAsync()
    {
        try
        {
            var (providerSettings, client) = Resolve();
            var project = await ResolveProjectAsync(client, providerSettings, allowCreate: false);

            const string query = """
                query($projectId: ID!) {
                  node(id: $projectId) {
                    ... on ProjectV2 {
                      items(first: 100) {
                        nodes {
                          id
                          content {
                            __typename
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

    public async Task<Response<TaskPromotionResult>> EnsureSubIssueAsync(string adrIssueNodeId, TaskRecord task, SyncLink? existingTaskLink, bool dryRun = false)
    {
        try
        {
            var (providerSettings, client) = Resolve();

            string projectItemId;
            string taskIssueNodeId;
            string contentType;
            string? url;
            bool promoted;

            if (existingTaskLink == null || string.IsNullOrWhiteSpace(existingTaskLink.ExternalId))
            {
                if (dryRun)
                {
                    return new Response<TaskPromotionResult>(true, $"[DRY RUN] Would create a new GitHub issue for task '{task.Title}' and attach it as a sub-issue.", new TaskPromotionResult
                    {
                        Promoted = true,
                        ExternalContentType = "Issue"
                    });
                }

                var repositoryId = await ResolveRepositoryIdAsync(client, providerSettings.TargetRepository);
                var project = await ResolveProjectAsync(client, providerSettings, allowCreate: true);
                var body = TaskBodyFormat.Combine(task.Description, task.Details);
                var newHash = ContentSyncMarker.ComputeHash(task.Title, body);
                (taskIssueNodeId, url) = await CreateIssueAsync(client, repositoryId, task.Title, ContentSyncMarker.AppendMarker(body, newHash));
                projectItemId = await AddProjectItemAsync(client, project.Id, taskIssueNodeId);
                contentType = "Issue";
                promoted = true;
            }
            else if (string.Equals(existingTaskLink.ExternalContentType, "DraftIssue", StringComparison.Ordinal))
            {
                if (dryRun)
                {
                    return new Response<TaskPromotionResult>(true, $"[DRY RUN] Would promote task '{task.Title}''s draft issue to a real GitHub issue and attach it as a sub-issue.", new TaskPromotionResult
                    {
                        Promoted = true,
                        ExternalId = existingTaskLink.ExternalId,
                        ExternalContentType = "DraftIssue"
                    });
                }

                var repositoryId = await ResolveRepositoryIdAsync(client, providerSettings.TargetRepository);
                projectItemId = existingTaskLink.ExternalId;
                (taskIssueNodeId, url) = await ConvertDraftIssueToIssueAsync(client, projectItemId, repositoryId);
                contentType = "Issue";
                promoted = true;
            }
            else
            {
                projectItemId = existingTaskLink.ExternalId;
                var (contentNodeId, _, _, remoteContentType, remoteUrl) = await FetchItemContentAsync(client, projectItemId);
                taskIssueNodeId = contentNodeId;
                url = remoteUrl;
                contentType = remoteContentType;
                promoted = false;

                if (dryRun)
                {
                    return new Response<TaskPromotionResult>(true, $"[DRY RUN] Would attach task '{task.Title}' (already a real {remoteContentType}) as a sub-issue.", new TaskPromotionResult
                    {
                        Promoted = false,
                        ExternalId = projectItemId,
                        ExternalContentType = contentType,
                        ExternalUrl = url
                    });
                }
            }

            await AddSubIssueAsync(client, adrIssueNodeId, taskIssueNodeId);

            return new Response<TaskPromotionResult>(true, null, new TaskPromotionResult
            {
                Promoted = promoted,
                ExternalId = projectItemId,
                ExternalContentType = contentType,
                ExternalUrl = url,
                SubIssueLinked = true
            });
        }
        catch (Exception ex) when (ex is GitHubGraphQlException or InvalidOperationException)
        {
            return new Response<TaskPromotionResult>(false, ex.Message, new TaskPromotionResult());
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
                $"GitHub Projects ADR sync requires a personal access token (with repository/issue write scope) in the {patEnvironmentVariable} environment variable. " +
                $"Set \"sync.syncPatName\" in adr.config.json to read from a different variable, e.g. when this machine works across multiple repositories with different tokens.");
        }

        return (providerSettings, new GitHubGraphQlClient(new HttpClient(), pat));
    }

    private static async Task<string> ResolveRepositoryIdAsync(IGitHubGraphQlClient client, string targetRepository)
    {
        var parts = targetRepository.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                "GitHub Projects ADR sync requires \"sync.settings.targetRepository\" in \"owner/repo\" form in adr.config.json.");
        }

        const string query = """
            query($owner: String!, $name: String!) {
              repository(owner: $owner, name: $name) { id }
            }
            """;

        var data = await client.ExecuteAsync(query, new { owner = parts[0], name = parts[1] });
        if (!data.TryGetProperty("repository", out var repositoryElement) || repositoryElement.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidOperationException($"GitHub repository \"{targetRepository}\" was not found or is not accessible with the configured token.");
        }

        return repositoryElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("GitHub response was missing the repository id.");
    }

    private static async Task<ProjectInfo> ResolveProjectAsync(IGitHubGraphQlClient client, GitHubProjectsSettings providerSettings, bool allowCreate)
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

        string? adrStatusFieldId = null;
        var optionIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in projectElement.GetProperty("fields").GetProperty("nodes").EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("name", out var nameElement))
            {
                continue;
            }

            if (!string.Equals(nameElement.GetString(), providerSettings.AdrStatusFieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!field.TryGetProperty("options", out var optionsElement) || optionsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"GitHub Projects field \"{providerSettings.AdrStatusFieldName}\" is not a single-select field, so it cannot be used for ADR status mapping.");
            }

            adrStatusFieldId = field.GetProperty("id").GetString();
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

        if (adrStatusFieldId == null && allowCreate)
        {
            // Auto-create the field, seeded with one option per AdrStatus value, the first time this
            // board is used for ADR sync - mirrors this codebase's existing "create on first use"
            // convention (e.g. markdown templates). See ADR 00010. Never fires when allowCreate is
            // false (dry runs, DiscoverItemsAsync) - those must never mutate anything; a still-missing
            // field there just leaves AdrStatusFieldId null, which callers already handle gracefully.
            var created = await CreateAdrStatusFieldAsync(client, projectId, providerSettings.AdrStatusFieldName);
            adrStatusFieldId = created.FieldId;
            optionIdsByName = created.OptionIdsByName;
        }

        return new ProjectInfo(projectId, projectUrl, adrStatusFieldId, optionIdsByName);
    }

    /// <summary>
    /// Creates <paramref name="fieldName" /> as a single-select field on the project, seeded with one
    /// option per <see cref="AdrStatus" /> value using its enum name verbatim - matching the common
    /// case where <see cref="GitHubProjectsSettings.AdrImportStatusMap" />/
    /// <see cref="GitHubProjectsSettings.AdrExportStatusMap" /> map 1:1 by name. A repository whose
    /// board already uses different option names should create the field manually instead and map
    /// accordingly - this only fires when the field is entirely absent.
    /// </summary>
    private static async Task<(string FieldId, Dictionary<string, string> OptionIdsByName)> CreateAdrStatusFieldAsync(IGitHubGraphQlClient client, string projectId, string fieldName)
    {
        var options = DefaultAdrStatusOptions.Select(o => new { name = o.Name, color = o.Color, description = "" }).ToArray();

        const string mutation = """
            mutation($projectId: ID!, $name: String!, $options: [ProjectV2SingleSelectFieldOptionInput!]!) {
              createProjectV2Field(input: { projectId: $projectId, dataType: SINGLE_SELECT, name: $name, singleSelectOptions: $options }) {
                projectV2Field {
                  ... on ProjectV2SingleSelectField { id options { id name } }
                }
              }
            }
            """;

        var data = await client.ExecuteAsync(mutation, new { projectId, name = fieldName, options });
        var field = data.GetProperty("createProjectV2Field").GetProperty("projectV2Field");
        var fieldId = field.GetProperty("id").GetString() ?? throw new InvalidOperationException("GitHub did not return an id for the created ADR status field.");

        var optionIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in field.GetProperty("options").EnumerateArray())
        {
            var optionName = option.GetProperty("name").GetString();
            var optionId = option.GetProperty("id").GetString();
            if (optionName != null && optionId != null)
            {
                optionIdsByName[optionName] = optionId;
            }
        }

        return (fieldId, optionIdsByName);
    }

    private static async Task<(string IssueId, string? Url)> CreateIssueAsync(IGitHubGraphQlClient client, string repositoryId, string title, string body)
    {
        const string mutation = """
            mutation($repositoryId: ID!, $title: String!, $body: String!) {
              createIssue(input: { repositoryId: $repositoryId, title: $title, body: $body }) {
                issue { id url }
              }
            }
            """;

        var data = await client.ExecuteAsync(mutation, new { repositoryId, title, body });
        var issue = data.GetProperty("createIssue").GetProperty("issue");
        var issueId = issue.GetProperty("id").GetString() ?? throw new InvalidOperationException("GitHub did not return an id for the created issue.");
        var url = issue.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        return (issueId, url);
    }

    private static async Task<string> AddProjectItemAsync(IGitHubGraphQlClient client, string projectId, string contentId)
    {
        const string mutation = """
            mutation($projectId: ID!, $contentId: ID!) {
              addProjectV2ItemById(input: { projectId: $projectId, contentId: $contentId }) {
                item { id }
              }
            }
            """;

        var data = await client.ExecuteAsync(mutation, new { projectId, contentId });
        return data.GetProperty("addProjectV2ItemById").GetProperty("item").GetProperty("id").GetString()
            ?? throw new InvalidOperationException("GitHub Projects did not return an item id for the added issue.");
    }

    private static async Task<(string IssueId, string? Url)> ConvertDraftIssueToIssueAsync(IGitHubGraphQlClient client, string projectItemId, string repositoryId)
    {
        const string mutation = """
            mutation($itemId: ID!, $repositoryId: ID!) {
              convertProjectV2DraftIssueItemToIssue(input: { itemId: $itemId, repositoryId: $repositoryId }) {
                item {
                  content {
                    ... on Issue { id url }
                  }
                }
              }
            }
            """;

        var data = await client.ExecuteAsync(mutation, new { itemId = projectItemId, repositoryId });
        var content = data.GetProperty("convertProjectV2DraftIssueItemToIssue").GetProperty("item").GetProperty("content");
        var issueId = content.GetProperty("id").GetString() ?? throw new InvalidOperationException("GitHub did not return an issue id after converting the draft issue.");
        var url = content.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        return (issueId, url);
    }

    /// <summary>
    /// Fetches a project item's current content node id (the Issue's/PR's own id, not its project-item
    /// id - needed for <see cref="AddSubIssueAsync" />), title, and body (still carrying its trailing
    /// hash marker, if any). ADR items are always real issues/PRs from this provider, never draft
    /// issues.
    /// </summary>
    private static async Task<(string ContentNodeId, string Title, string Body, string ContentType, string? Url)> FetchItemContentAsync(IGitHubGraphQlClient client, string itemId)
    {
        const string query = """
            query($itemId: ID!) {
              node(id: $itemId) {
                ... on ProjectV2Item {
                  content {
                    __typename
                    ... on Issue { id title body url }
                    ... on PullRequest { id title body url }
                  }
                }
              }
            }
            """;

        var data = await client.ExecuteAsync(query, new { itemId });
        var content = data.GetProperty("node").GetProperty("content");
        if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("__typename", out var typeNameElement))
        {
            throw new InvalidOperationException($"GitHub Projects item {itemId} has no readable content (not an issue or pull request).");
        }

        var contentNodeId = content.GetProperty("id").GetString() ?? throw new InvalidOperationException($"GitHub Projects item {itemId}'s content had no id.");
        var title = content.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? string.Empty : string.Empty;
        var body = content.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
        var contentType = typeNameElement.GetString() ?? string.Empty;
        var url = content.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        return (contentNodeId, title, body, contentType, url);
    }

    private static async Task UpdateIssueContentAsync(IGitHubGraphQlClient client, string issueId, string title, string body)
    {
        const string mutation = """
            mutation($issueId: ID!, $title: String!, $body: String!) {
              updateIssue(input: { id: $issueId, title: $title, body: $body }) {
                issue { id }
              }
            }
            """;

        await client.ExecuteAsync(mutation, new { issueId, title, body });
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

    /// <summary>
    /// Attaches <paramref name="subIssueId" /> as a sub-issue of <paramref name="issueId" />, using
    /// GitHub's now-GA sub-issue relationship. The exact current requirements (whether a
    /// <c>GraphQL-Features</c> preview header is still needed post-GA) were not fully confirmed at
    /// the time this was written - see ADR 00010's Consequences.
    /// </summary>
    private static async Task AddSubIssueAsync(IGitHubGraphQlClient client, string issueId, string subIssueId)
    {
        const string mutation = """
            mutation($issueId: ID!, $subIssueId: ID!) {
              addSubIssue(input: { issueId: $issueId, subIssueId: $subIssueId }) {
                issue { id }
                subIssue { id }
              }
            }
            """;

        await client.ExecuteAsync(mutation, new { issueId, subIssueId });
    }

    private sealed record ProjectInfo(string Id, string Url, string? AdrStatusFieldId, Dictionary<string, string> AdrStatusOptionIdsByName);
}
