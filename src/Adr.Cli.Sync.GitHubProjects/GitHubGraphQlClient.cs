using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// Calls the GitHub GraphQL API (<c>https://api.github.com/graphql</c>) using a personal access
/// token. Retry/backoff behavior is intentionally left minimal here, per ADR 00008's decision to
/// leave provider-specific error handling to the implementation rather than specify it further.
/// </summary>
public class GitHubGraphQlClient : IGitHubGraphQlClient
{
    private const string Endpoint = "https://api.github.com/graphql";

    private readonly HttpClient httpClient;

    public GitHubGraphQlClient(HttpClient httpClient, string personalAccessToken)
    {
        this.httpClient = httpClient;
        this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
        if (!this.httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("adr-cli"))
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("adr-cli");
        }
    }

    public async Task<JsonElement> ExecuteAsync(string query, object variables)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(Endpoint, new { query, variables });
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubGraphQlException($"Could not reach the GitHub GraphQL API: {ex.Message}", ex);
        }

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubGraphQlException($"GitHub GraphQL API returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var firstMessage = errors[0].TryGetProperty("message", out var messageElement) ? messageElement.GetString() : errors.ToString();
            throw new GitHubGraphQlException($"GitHub GraphQL API reported an error: {firstMessage}");
        }

        if (!root.TryGetProperty("data", out var data))
        {
            throw new GitHubGraphQlException("GitHub GraphQL API response had no \"data\" property.");
        }

        return data.Clone();
    }
}
