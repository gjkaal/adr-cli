using System;

namespace Adr.Cli.Sync.GitHubProjects;

/// <summary>
/// A GitHub GraphQL request failed: transport-level, a non-success HTTP status, or a top-level
/// <c>errors</c> array in an otherwise-200 response.
/// </summary>
public class GitHubGraphQlException : Exception
{
    public GitHubGraphQlException(string message) : base(message)
    {
    }

    public GitHubGraphQlException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
