using Adr.Cli.CommandHandlers;
using Adr.Cli.Sync;

using Microsoft.Extensions.Logging.Abstractions;

using System.Threading.Tasks;

using Xunit;

namespace Adr.Cli.Sync.GitHubProjects;

public sealed class WithGitHubProjectsAdrSyncProvider
{
    private const string ProjectResolveResponse = """
        {
          "user": {
            "projectV2": {
              "id": "PROJ1",
              "url": "https://github.com/users/gjkaal/projects/2",
              "fields": {
                "nodes": [
                  { "id": "FIELD1", "name": "ADR Status", "options": [
                    { "id": "OID_PROPOSED", "name": "Proposed" },
                    { "id": "OID_ACCEPTED", "name": "Accepted" }
                  ] }
                ]
              }
            }
          }
        }
        """;

    private const string RepositoryResolveResponse = """{ "repository": { "id": "REPO1" } }""";

    private const string SettingsJson = """
        {
          "ownerType": "User",
          "owner": "gjkaal",
          "projectNumber": 2,
          "targetRepository": "gjkaal/adr-cli",
          "adrStatusFieldName": "ADR Status",
          "adrImportStatusMap": { "Accepted": "Accepted" },
          "adrExportStatusMap": { "Proposed": "Proposed" }
        }
        """;

    private const string LocalTitle = "Use a message bus";
    private static readonly string LocalBody = AdrBodyFormat.Combine("Context text.", "Decision text.", "Consequences text.");

    private static AdrRecord NewAdr(AdrStatus status = AdrStatus.Proposed) => new()
    {
        RecordId = 10,
        Title = LocalTitle,
        Context = "Context text.",
        Decision = "Decision text.",
        Consequences = "Consequences text.",
        Status = status
    };

    private static TaskRecord NewTask(int id = 1) => new()
    {
        RecordId = id,
        Title = "Related task",
        Description = "Do the thing.",
        Details = "More detail."
    };

    private static string IssueContentResponse(string title, string body, string contentType = "Issue", string issueId = "ISSUE1")
    {
        var bodyEscaped = body.Replace("\n", "\\n").Replace("\"", "\\\"");
        var titleEscaped = title.Replace("\"", "\\\"");
        return $$"""{ "node": { "content": { "__typename": "{{contentType}}", "id": "{{issueId}}", "title": "{{titleEscaped}}", "body": "{{bodyEscaped}}", "url": "https://github.com/gjkaal/adr-cli/issues/1" } } }""";
    }

    private static string IssueContentResponseWithMarker(string title, string content, string markerHash, string contentType = "Issue", string issueId = "ISSUE1")
    {
        return IssueContentResponse(title, ContentSyncMarker.AppendMarker(content, markerHash), contentType, issueId);
    }

    [Fact]
    public async Task ExportAsync_NoExistingLink_CreatesIssueAddsToBoardAndSetsMappedStatus()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            RepositoryResolveResponse,
            """{ "createIssue": { "issue": { "id": "ISSUE1", "url": "https://github.com/gjkaal/adr-cli/issues/1" } } }""",
            """{ "addProjectV2ItemById": { "item": { "id": "ITEM1" } } }""",
            """{ "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewAdr(), existingLink: null);

        Assert.True(response.Success);
        Assert.True(response.Value!.Created);
        Assert.Equal("ITEM1", response.Value.ExternalId);
        Assert.Equal("ISSUE1", response.Value.IssueNodeId);
        Assert.Equal("Issue", response.Value.ExternalContentType);
        Assert.Equal(SyncState.Synced, response.Value.SyncState);
    }

    [Fact]
    public async Task ExportAsync_MissingAdrStatusField_AutoCreatesFieldWithSeededOptionsAndProceeds()
    {
        const string projectWithoutAdrStatusFieldResponse = """
            {
              "user": {
                "projectV2": {
                  "id": "PROJ1",
                  "url": "https://github.com/users/gjkaal/projects/2",
                  "fields": { "nodes": [ { "id": "FIELD0", "name": "Status", "options": [] } ] }
                }
              }
            }
            """;
        const string createFieldResponse = """
            {
              "createProjectV2Field": {
                "projectV2Field": {
                  "id": "FIELD1",
                  "options": [
                    { "id": "OID_NEW", "name": "New" },
                    { "id": "OID_PROPOSED", "name": "Proposed" },
                    { "id": "OID_FINAL", "name": "Final" },
                    { "id": "OID_ACCEPTED", "name": "Accepted" },
                    { "id": "OID_ERROR", "name": "Error" },
                    { "id": "OID_OBSOLETE", "name": "Obsolete" }
                  ]
                }
              }
            }
            """;

        var client = new FakeGitHubGraphQlClient(
            projectWithoutAdrStatusFieldResponse,
            createFieldResponse,
            RepositoryResolveResponse,
            """{ "createIssue": { "issue": { "id": "ISSUE1", "url": "https://github.com/gjkaal/adr-cli/issues/1" } } }""",
            """{ "addProjectV2ItemById": { "item": { "id": "ITEM1" } } }""",
            """{ "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewAdr(), existingLink: null);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Synced, response.Value!.SyncState);
        Assert.Equal(6, client.Calls.Count);
    }

    [Fact]
    public async Task ExportAsync_ExistingLinkRemoteUnchanged_UpdatesIssueInsteadOfCreating()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            IssueContentResponse(LocalTitle, LocalBody),
            """{ "updateIssue": { "issue": { "id": "ISSUE1" } } }""",
            """{ "updateProjectV2ItemFieldValue": { "projectV2Item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsAdrSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ExportAsync(NewAdr(), existingLink);

        Assert.True(response.Success);
        Assert.False(response.Value!.Created);
        Assert.Equal("ISSUE1", response.Value.IssueNodeId);
        Assert.Equal(SyncState.Synced, response.Value.SyncState);
    }

    [Fact]
    public async Task ExportAsync_RemoteDivergedSinceLastSync_RefusesToPushAndReportsMismatch()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            IssueContentResponse(LocalTitle, "Someone edited this on GitHub.\n[HASH:doesnotmatch]"));
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsAdrSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ExportAsync(NewAdr(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Mismatch, response.Value!.SyncState);
        Assert.False(response.Value.Created);
        // Only the project resolve + content fetch happened - no update/status mutation was sent.
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task ExportAsync_LocalStatusNotInExportMap_LeavesSyncStateUnmapped()
    {
        var client = new FakeGitHubGraphQlClient(
            ProjectResolveResponse,
            RepositoryResolveResponse,
            """{ "createIssue": { "issue": { "id": "ISSUE1", "url": "https://github.com/gjkaal/adr-cli/issues/1" } } }""",
            """{ "addProjectV2ItemById": { "item": { "id": "ITEM1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewAdr(AdrStatus.Final), existingLink: null);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Unmapped, response.Value!.SyncState);
    }

    [Fact]
    public async Task ImportAsync_RemoteContentChangedLocalUnchanged_PullsRemoteContent()
    {
        var lastSyncedHash = ContentSyncMarker.ComputeHash(LocalTitle, LocalBody);
        var updatedBody = AdrBodyFormat.Combine("New context.", "New decision.", "New consequences.");
        var client = new FakeGitHubGraphQlClient(
            IssueContentResponseWithMarker(LocalTitle, updatedBody, lastSyncedHash),
            """{ "node": { "fieldValueByName": { "name": "Accepted" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsAdrSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ImportAsync(NewAdr(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(LocalTitle, response.Value!.PulledTitle);
        Assert.Equal(updatedBody, response.Value.PulledBody);
        Assert.Equal(AdrStatus.Accepted, response.Value.MappedStatus);
    }

    [Fact]
    public async Task ImportAsync_LocalChangedSinceLastSync_ReturnsMismatchAndDoesNotReadStatus()
    {
        // Self-consistent marker for different title/content than local (not "no marker at all") -
        // simulates a stale baseline from an earlier sync, which is what actually triggers Mismatch;
        // a body with no marker at all has no baseline to compare against and resolves as Pull instead.
        const string otherTitle = "Some other title entirely";
        const string otherContent = "Some other content entirely.";
        var client = new FakeGitHubGraphQlClient(
            IssueContentResponseWithMarker(otherTitle, otherContent, ContentSyncMarker.ComputeHash(otherTitle, otherContent)));
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);
        var existingLink = new SyncLink { Provider = GitHubProjectsAdrSyncProvider.ProviderName, ExternalId = "ITEM1" };

        var response = await provider.ImportAsync(NewAdr(), existingLink);

        Assert.True(response.Success);
        Assert.Equal(SyncState.Mismatch, response.Value!.SyncState);
        Assert.Null(response.Value.MappedStatus);
        Assert.Single(client.Calls);
    }

    [Fact]
    public async Task EnsureSubIssueAsync_NoExistingTaskLink_CreatesIssueAddsToBoardAndAttachesSubIssue()
    {
        var client = new FakeGitHubGraphQlClient(
            RepositoryResolveResponse,
            ProjectResolveResponse,
            """{ "createIssue": { "issue": { "id": "TASK_ISSUE1", "url": "https://github.com/gjkaal/adr-cli/issues/2" } } }""",
            """{ "addProjectV2ItemById": { "item": { "id": "TASK_ITEM1" } } }""",
            """{ "addSubIssue": { "issue": { "id": "ADR_ISSUE1" }, "subIssue": { "id": "TASK_ISSUE1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);

        var response = await provider.EnsureSubIssueAsync("ADR_ISSUE1", NewTask(), existingTaskLink: null);

        Assert.True(response.Success);
        Assert.True(response.Value!.Promoted);
        Assert.True(response.Value.SubIssueLinked);
        Assert.Equal("TASK_ITEM1", response.Value.ExternalId);
        Assert.Equal("Issue", response.Value.ExternalContentType);
    }

    [Fact]
    public async Task EnsureSubIssueAsync_ExistingDraftIssue_PromotesAndAttaches()
    {
        var client = new FakeGitHubGraphQlClient(
            RepositoryResolveResponse,
            """{ "convertProjectV2DraftIssueItemToIssue": { "item": { "content": { "id": "TASK_ISSUE1", "url": "https://github.com/gjkaal/adr-cli/issues/2" } } } }""",
            """{ "addSubIssue": { "issue": { "id": "ADR_ISSUE1" }, "subIssue": { "id": "TASK_ISSUE1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);
        var existingTaskLink = new SyncLink { Provider = GitHubProjectsAdrSyncProvider.ProviderName, ExternalId = "TASK_ITEM1", ExternalContentType = "DraftIssue" };

        var response = await provider.EnsureSubIssueAsync("ADR_ISSUE1", NewTask(), existingTaskLink);

        Assert.True(response.Success);
        Assert.True(response.Value!.Promoted);
        Assert.Equal("TASK_ITEM1", response.Value.ExternalId);
        Assert.Equal("Issue", response.Value.ExternalContentType);
    }

    [Fact]
    public async Task EnsureSubIssueAsync_ExistingRealIssue_ReusesAsIsAndAttaches()
    {
        var client = new FakeGitHubGraphQlClient(
            IssueContentResponse("Related task", "Body without marker.", contentType: "Issue", issueId: "TASK_ISSUE1"),
            """{ "addSubIssue": { "issue": { "id": "ADR_ISSUE1" }, "subIssue": { "id": "TASK_ISSUE1" } } }""");
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);
        var existingTaskLink = new SyncLink { Provider = GitHubProjectsAdrSyncProvider.ProviderName, ExternalId = "TASK_ITEM1", ExternalContentType = "Issue" };

        var response = await provider.EnsureSubIssueAsync("ADR_ISSUE1", NewTask(), existingTaskLink);

        Assert.True(response.Success);
        Assert.False(response.Value!.Promoted);
        Assert.Equal("TASK_ITEM1", response.Value.ExternalId);
    }

    [Fact]
    public async Task EnsureSubIssueAsync_DryRun_ReportsWithoutCallingSubIssueMutation()
    {
        var client = new FakeGitHubGraphQlClient();
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(SettingsJson), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);

        var response = await provider.EnsureSubIssueAsync("ADR_ISSUE1", NewTask(), existingTaskLink: null, dryRun: true);

        Assert.True(response.Success);
        Assert.True(response.Value!.Promoted);
        Assert.False(response.Value.SubIssueLinked);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task ExportAsync_MissingTargetRepository_FailsWithActionableMessage()
    {
        var settingsWithoutRepo = """
            {
              "ownerType": "User",
              "owner": "gjkaal",
              "projectNumber": 2,
              "adrStatusFieldName": "ADR Status"
            }
            """;
        var client = new FakeGitHubGraphQlClient(ProjectResolveResponse);
        var provider = new GitHubProjectsAdrSyncProvider(new FixedSyncSettings(settingsWithoutRepo), NullLogger<GitHubProjectsAdrSyncProvider>.Instance, client);

        var response = await provider.ExportAsync(NewAdr(), existingLink: null);

        Assert.False(response.Success);
        Assert.Contains("targetRepository", response.Message);
    }
}
