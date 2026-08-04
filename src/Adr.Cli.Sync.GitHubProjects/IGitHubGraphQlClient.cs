using System.Text.Json;
using System.Threading.Tasks;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// Thin seam over the GitHub GraphQL API so <see cref="GitHubProjectsTaskSyncProvider" /> can be
/// unit tested without a real HTTP call.
/// </summary>
public interface IGitHubGraphQlClient
{
    /// <summary>
    /// Execute a GraphQL query or mutation and return its <c>data</c> object.
    /// </summary>
    /// <exception cref="GitHubGraphQlException">
    /// The request failed transport-level, returned a non-success status, or the response contained
    /// a top-level <c>errors</c> array.
    /// </exception>
    Task<JsonElement> ExecuteAsync(string query, object variables);
}
