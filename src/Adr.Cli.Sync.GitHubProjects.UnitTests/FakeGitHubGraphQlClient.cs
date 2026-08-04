using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// Returns canned responses in call order, so tests can assert on the sequence of GraphQL
/// operations the provider issues without making real HTTP calls.
/// </summary>
internal sealed class FakeGitHubGraphQlClient : IGitHubGraphQlClient
{
    private readonly Queue<JsonElement> responses;

    public List<(string Query, object Variables)> Calls { get; } = new();

    public FakeGitHubGraphQlClient(params string[] jsonResponses)
    {
        responses = new Queue<JsonElement>(jsonResponses.Select(json => JsonDocument.Parse(json).RootElement.Clone()));
    }

    public Task<JsonElement> ExecuteAsync(string query, object variables)
    {
        Calls.Add((query, variables));
        return Task.FromResult(responses.Dequeue());
    }
}
