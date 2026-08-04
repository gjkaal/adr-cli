using Adr.Cli.CommandHandlers;
using Adr.Cli.Sync;

using Microsoft.Extensions.Logging.Abstractions;

using System.Threading.Tasks;

using Xunit;

namespace Adr.Cli.Sync.GitHubProjects;

public sealed class WithGitHubProjectsTaskSyncProvider
{
    private const string ProjectResolveResponse = """
        {
          "user": {
            "projectV2": {
              "id": "PROJ1",
              "url": "https://github.com/users/gjkaal/projects/2",
              "fields": {
                "nodes": [
                  { "id": "FIELD1", "name": "Status", "options": [
                    { "id": "OID_TODO", "name": "Todo" },
                    { "id": "OID_DOING", "name": "Doing" }
                  ] }
                ]
              }
            }
          }
        }
        """;

    private const string SettingsJson = """
        {
          "ownerType": "User",
          "owner": "gjkaal",
          "projectNumber": 2,
          "statusFieldName": "Status",
          "importStatusMap": { "Doing": "Active" },
          "exportStatusMap": { "New": "Todo" }
        }
        """;

    private const string LocalTitle = "Wire up CI";
    private const string LocalBody = "Automate build and test.\n\n---\n\nUse GitHub Actions.";

    private static TaskRecord NewTask(PlanningStatus status = PlanningStatus.New) => new()
    {
        RecordId = 1,
        Title = LocalTitle,
        Description = "Automate build and test.",
        Details = "Use GitHub Actions.",
        Status = status
    };

    /// <summary>
    /// Builds a fake "current remote content" response that is self-consistent with its own embedded
    /// marker (i.e. nobody has touched it since it was last pushed) for whatever title/content is
    /// given - so tests can construct exactly the scenario they want without hardcoding a hash.
    /// </summary>
    private static string DraftIssueContentResponse(string title, string content)
    {
        return DraftIssueContentResponseWithMarker(title, content, ContentSyncMarker.ComputeHash(title, content));
    }

    /// <summary>
    /// Like <see cref="DraftIssueContentResponse" />, but lets the test embed a marker hash that
    /// doesn't match the given title/content - simulating either a baseline from an earlier sync
    /// (remote has since moved away from it) or a genuinely stale/foreign marker.
    /// </summary>
    private static string DraftIssueContentResponseWithMarker(string title, string content, string markerHash)
    {
        var bodyWithMarker = ContentSyncMarker.AppendMarker(content, markerHash).Replace("\n", "\\n").Replace("\"", "\\\"");
        var escapedTitle = title.Replace("\"", "\\\"");
        return $$"""{ "node": { "content": { "__typename": "DraftIssue", "id": "DRAFT1", "title": "{{escapedTitle}}", "body": "{{bodyWithMarker}}" } } }""";
    }

    [Fact]
    public async Task ExportAsync_NoExistingLink_CreatesDraftIssueAndSetsMappedStatus()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            """{ "addProjectV2DraftIssue": { "projectItem": { "id": "ITEM1" } } }""",
            """{ "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewTask(), existingLink: null);

        Assert.True(response.Success);
        Assert.True(response.Value!.Created);
        Assert.Equal("ITEM1", response.Value.ExternalId);
        Assert.Equal("gjkaal/2", response.Value.ExternalScope);
        Assert.Equal(SyncState.Synced, response.Value.SyncState);
        Assert.Equal(3, client.Calls.Count);
    }

    [Fact]
    public async Task ExportAsync_ExistingLinkRemoteUnchanged_UpdatesDraftIssueInsteadOfCreating()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            DraftIssueContentResponse(LocalTitle, LocalBody),
            """{ "updateProjectV2DraftIssue": { "draftIssue": { "id": "DRAFT1" } } }""",
            """{ "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ExportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.False(response.Value!.Created);
        Assert.Equal("ITEM1", response.Value.ExternalId);
        Assert.Equal(SyncState.Synced, response.Value.SyncState);
    }

    [Fact]
    public async Task ExportAsync_RemoteDivergedSinceLastSync_RefusesToPushAndReportsMismatch()
    {
        // The embedded marker doesn't match the remote's own current content - someone edited it on
        // GitHub without going through this tool.
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            """{ "node": { "content": { "__typename": "DraftIssue", "id": "DRAFT1", "title": "Wire up CI", "body": "Someone edited this on GitHub.\n[HASH:doesnotmatch]" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ExportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Mismatch, response.Value!.SyncState);
        Assert.False(response.Value.Created);
        // Only the project resolve + content fetch happened - no update/status mutation was sent.
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task ExportAsync_ExistingLinkIsRealIssue_SkipsContentPushButStillPushesStatus()
    {
        // A real repository issue (e.g. someone added it to the board directly), not a draft issue
        // this connector created - content can't be pushed without repo write permissions, but
        // status still can be, since field updates work on any item type.
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            """{ "node": { "content": { "__typename": "Issue", "title": "Wire up CI", "body": "Real issue body, no marker." } } }""",
            """{ "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ExportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Synced, response.Value!.SyncState);
        // project resolve + content fetch + status field mutation - no draft-issue update call.
        Assert.Equal(3, client.Calls.Count);
    }

    [Fact]
    public async Task ExportAsync_LocalStatusNotInExportMap_LeavesSyncStateUnmappedAndSkipsFieldMutation()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            """{ "addProjectV2DraftIssue": { "projectItem": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewTask(PlanningStatus.Active), existingLink: null);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Unmapped, response.Value!.SyncState);
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task ImportAsync_ContentAndStatusUnchanged_ReturnsMappedStatusWithNoPulledContent()
    {
        var client = new FakeGitHubGraphQlClient(
            DraftIssueContentResponse(LocalTitle, LocalBody),
            """{ "node": { "fieldValueByName": { "name": "Doing" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ImportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(PlanningStatus.Active, response.Value!.MappedStatus);
        Assert.Equal(SyncState.Synced, response.Value.SyncState);
        Assert.Equal("Doing", response.Value.ExternalStatusRaw);
        Assert.Null(response.Value.PulledTitle);
        Assert.Null(response.Value.PulledBody);
    }

    [Fact]
    public async Task ImportAsync_ExternalStatusNotInMap_LeavesSyncStateUnmapped()
    {
        var client = new FakeGitHubGraphQlClient(
            DraftIssueContentResponse(LocalTitle, LocalBody),
            """{ "node": { "fieldValueByName": { "name": "Blocked" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ImportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.Null(response.Value!.MappedStatus);
        Assert.Equal(SyncState.Unmapped, response.Value.SyncState);
    }

    [Fact]
    public async Task ImportAsync_RemoteContentChangedLocalUnchanged_PullsRemoteContent()
    {
        // The embedded marker matches current LOCAL content (the baseline from the last sync, since
        // local hasn't moved) while the actual remote body has since changed to something else.
        var lastSyncedHash = ContentSyncMarker.ComputeHash(LocalTitle, LocalBody);
        var client = new FakeGitHubGraphQlClient(
            DraftIssueContentResponseWithMarker(LocalTitle, "Someone edited this description on GitHub.", lastSyncedHash),
            """{ "node": { "fieldValueByName": { "name": "Doing" } } }""");
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ImportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(LocalTitle, response.Value!.PulledTitle);
        Assert.Equal("Someone edited this description on GitHub.", response.Value.PulledBody);
        // Status is still read/mapped normally alongside the content pull.
        Assert.Equal(PlanningStatus.Active, response.Value.MappedStatus);
    }

    [Fact]
    public async Task ImportAsync_LocalChangedSinceLastSync_ReturnsMismatchAndDoesNotReadStatus()
    {
        // Marker embeds a hash for different content than what's now local - local moved since the
        // last sync, so we can't trust an automatic resolution regardless of what remote did.
        var client = new FakeGitHubGraphQlClient(
            DraftIssueContentResponse("Some other title entirely", "Some other content entirely."));
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ImportAsync(NewTask(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Mismatch, response.Value!.SyncState);
        Assert.Null(response.Value.MappedStatus);
        // Only the content fetch happened - status was never read once a mismatch was detected.
        Assert.Single(client.Calls);
    }

    [Fact]
    public async Task ImportAsync_NoExternalId_FailsWithoutCallingProvider()
    {
        var client = new FakeGitHubGraphQlClient();
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsTaskSyncProvider.ProviderName, ExternalId = "" };

        var response = await provider.ImportAsync(NewTask(), existingLink);

        Assert.False(response.Success);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task ExportAsync_MissingOwnerConfiguration_FailsWithActionableMessage()
    {
        var client = new FakeGitHubGraphQlClient();
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings("{}"), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewTask(), existingLink: null);

        Assert.False(response.Success);
        Assert.Contains("sync.settings", response.Message);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task DiscoverItemsAsync_ReturnsItemsWithMarkerFlag()
    {
        var itemsResponse = """
            {
              "node": {
                "items": {
                  "nodes": [
                    { "id": "ITEM1", "content": { "__typename": "DraftIssue", "title": "Has marker", "body": "Body text.\n[HASH:abc123]" } },
                    { "id": "ITEM2", "content": { "__typename": "Issue", "title": "No marker", "body": "Body text without a marker." } },
                    { "id": "ITEM3", "content": null }
                  ]
                }
              }
            }
            """;
        var client = new FakeGitHubGraphQlClient(ProjectResolveResponse, itemsResponse);
        var provider = new GitHubProjectsTaskSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsTaskSyncProvider>.Instance, client);

        var response = await provider.DiscoverItemsAsync();

        Assert.True(response.Success);
        Assert.Equal(2, response.Value!.Count);

        var withMarker = response.Value[0];
        Assert.Equal("Has marker", withMarker.Title);
        Assert.Equal("Body text.", withMarker.Body);
        Assert.True(withMarker.HasMarker);

        var withoutMarker = response.Value[1];
        Assert.Equal("No marker", withoutMarker.Title);
        Assert.False(withoutMarker.HasMarker);
    }
}
