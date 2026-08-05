using System.IO.Abstractions.TestingHelpers;

using Xunit;

namespace Adr.Cli;

public sealed class WithAdrSettings
{
    /// <summary>
    /// Regression test for a bug where <c>adr_init</c> on a brand new, empty folder reported
    /// "Initialization is already done" and never wrote the first ADR record.
    /// <c>RepositoryInitialized()</c> used to check <c>DocFolderInfo().Exists</c> - but
    /// <c>DocFolderInfo()</c> auto-creates the ADR folder (and an "Initialized.txt" marker) as a
    /// side effect of merely checking it, so the folder always reported as "existing" the instant
    /// it was first touched, regardless of whether any ADR had actually been written.
    /// </summary>
    [Fact]
    public void RepositoryInitialized_ReturnsFalse_ForFreshlyCreatedEmptyFolder()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\repo\empty");
        fileSystem.Directory.SetCurrentDirectory(@"C:\repo\empty");

        var settings = new AdrSettings(fileSystem);

        Assert.False(settings.RepositoryInitialized());

        // The folder auto-creation side effect is expected (and relied on elsewhere) - confirm it
        // still happened, so this test is exercising the actual trap and not just an early exit
        // before the folder existed at all.
        Assert.True(fileSystem.Directory.Exists(settings.DocFolderInfo().FullName));
    }

    [Fact]
    public void RepositoryInitialized_ReturnsTrue_OnceAnAdrRecordExists()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\repo\empty");
        fileSystem.Directory.SetCurrentDirectory(@"C:\repo\empty");

        var settings = new AdrSettings(fileSystem);
        var docFolder = settings.DocFolderInfo().FullName;
        fileSystem.File.WriteAllText(
            fileSystem.Path.Combine(docFolder, "00001-first-decision.md"),
            "# 00001. First decision");

        Assert.True(settings.RepositoryInitialized());
    }

    /// <summary>
    /// Regression test for the optional "ai" section in adr.config.json (see AI-Setup.md) - covers
    /// the shape actually written to this repo's own src/adr.config.json.
    /// </summary>
    [Fact]
    public void AiSettings_IsPopulated_FromConfigFileAiSection()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\repo\project");
        fileSystem.Directory.SetCurrentDirectory(@"C:\repo\project");
        fileSystem.File.WriteAllText(@"C:\repo\project\adr.config.json", """
        {
          "path": "doc\\adr",
          "templates": "doc\\templates",
          "tasks": "\\docs\\planning",
          "ai": {
            "provider": "AzureFoundry",
            "endpoint": "https://adr-cli-ai.cognitiveservices.azure.com/",
            "deploymentName": "gpt-4o"
          }
        }
        """);

        var settings = new AdrSettings(fileSystem);

        Assert.Equal("AzureFoundry", settings.AiSettings.Provider);
        Assert.Equal("https://adr-cli-ai.cognitiveservices.azure.com/", settings.AiSettings.Endpoint);
        Assert.Equal("gpt-4o", settings.AiSettings.DeploymentName);
    }

    /// <summary>
    /// AI drafting must stay opt-in: when adr.config.json has no "ai" section at all, AiSettings
    /// should come back empty (Provider = "") rather than throwing or defaulting to a provider.
    /// </summary>
    [Fact]
    public void AiSettings_IsEmpty_WhenConfigFileHasNoAiSection()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\repo\project");
        fileSystem.Directory.SetCurrentDirectory(@"C:\repo\project");
        fileSystem.File.WriteAllText(@"C:\repo\project\adr.config.json", """
        {
          "path": "doc\\adr",
          "templates": "doc\\templates"
        }
        """);

        var settings = new AdrSettings(fileSystem);

        Assert.Equal(string.Empty, settings.AiSettings.Provider);
        Assert.Equal(string.Empty, settings.AiSettings.Endpoint);
        Assert.Equal(string.Empty, settings.AiSettings.DeploymentName);
    }

    /// <summary>
    /// Unchanged precise behavior: pointing at (or inside) a directory that itself has an
    /// adr.config.json resolves immediately via the upward search - no downward scan involved.
    /// </summary>
    [Fact]
    public void TrySetContext_ResolvesImmediately_WhenDirectoryIsInsideARepository()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoA\src");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace\RepoA");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoA\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoA" }
        """);

        var settings = new AdrSettings(fileSystem);
        var result = settings.TrySetContext(@"C:\workspace\RepoA\src");

        Assert.True(result.Success);
        Assert.Equal("RepoA", result.ProjectName);
        Assert.Empty(result.Candidates);
    }

    /// <summary>
    /// New behavior: a workspace root with no adr.config.json of its own, but exactly one nested
    /// repository, auto-resolves to that repository instead of requiring disambiguation.
    /// </summary>
    [Fact]
    public void TrySetContext_SearchesDownward_AndAutoResolves_WhenExactlyOneNestedRepositoryFound()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoOnly");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoOnly\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoOnly" }
        """);

        var settings = new AdrSettings(fileSystem);
        var result = settings.TrySetContext(@"C:\workspace");

        Assert.True(result.Success);
        Assert.Equal("RepoOnly", result.ProjectName);
        Assert.Equal(@"C:\workspace\RepoOnly\adr.config.json", result.ConfigFilePath);
    }

    /// <summary>
    /// New behavior: a workspace root with multiple nested repositories returns candidates (project
    /// name + folder path for each) instead of guessing - this is the exact scenario that caused the
    /// n2adr / mediachoice incident (silently operating against the wrong project's ADRs).
    /// </summary>
    [Fact]
    public void TrySetContext_ReturnsCandidates_WhenMultipleNestedRepositoriesFound()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoA");
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoB");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoA\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoA" }
        """);
        fileSystem.File.WriteAllText(@"C:\workspace\RepoB\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoB" }
        """);

        var settings = new AdrSettings(fileSystem);
        var result = settings.TrySetContext(@"C:\workspace");

        Assert.False(result.Success);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Contains(result.Candidates, c => c.ProjectName == "RepoA" && c.FolderPath == @"C:\workspace\RepoA");
        Assert.Contains(result.Candidates, c => c.ProjectName == "RepoB" && c.FolderPath == @"C:\workspace\RepoB");
    }

    /// <summary>
    /// New behavior: set-context also accepts a project name instead of a path - resolved against
    /// the candidates from the most recent downward search (here, the ambiguous scan above).
    /// </summary>
    [Fact]
    public void TrySetContext_ResolvesByProjectName_UsingCandidatesFromPriorDownwardSearch()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoA");
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoB");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoA\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoA" }
        """);
        fileSystem.File.WriteAllText(@"C:\workspace\RepoB\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoB" }
        """);

        var settings = new AdrSettings(fileSystem);
        var ambiguous = settings.TrySetContext(@"C:\workspace");
        Assert.False(ambiguous.Success);

        var result = settings.TrySetContext("RepoB");

        Assert.True(result.Success);
        Assert.Equal("RepoB", result.ProjectName);
        Assert.Equal(@"C:\workspace\RepoB\adr.config.json", result.ConfigFilePath);
    }

    /// <summary>
    /// New behavior: a project-name lookup with no prior downward search runs a fresh one from the
    /// current root rather than failing outright.
    /// </summary>
    [Fact]
    public void TrySetContext_ResolvesByProjectName_RunningFreshDownwardSearch_WhenNoPriorCandidates()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoOnly");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoOnly\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoOnly" }
        """);

        var settings = new AdrSettings(fileSystem);
        var result = settings.TrySetContext("RepoOnly");

        Assert.True(result.Success);
        Assert.Equal("RepoOnly", result.ProjectName);
    }

    /// <summary>
    /// The downward search must not descend into build/dependency/hidden folders - scanning
    /// node_modules or bin/obj in a real repository would be slow and would never contain a
    /// legitimate adr.config.json anyway.
    /// </summary>
    [Fact]
    public void TrySetContext_DownwardSearch_SkipsBuildAndHiddenFolders()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoOnly");
        fileSystem.Directory.CreateDirectory(@"C:\workspace\node_modules\SomePackage");
        fileSystem.Directory.CreateDirectory(@"C:\workspace\.git\refs");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoOnly\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoOnly" }
        """);
        // A config file placed under a skipped folder must never surface as a candidate.
        fileSystem.File.WriteAllText(@"C:\workspace\node_modules\SomePackage\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "ShouldBeIgnored" }
        """);

        var settings = new AdrSettings(fileSystem);
        var result = settings.TrySetContext(@"C:\workspace");

        Assert.True(result.Success);
        Assert.Equal("RepoOnly", result.ProjectName);
    }

    /// <summary>
    /// A value that is neither an existing directory nor a known project name must fail with a
    /// clear message rather than throwing or silently leaving stale settings in place.
    /// </summary>
    [Fact]
    public void TrySetContext_Fails_ForUnknownProjectName()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(@"C:\workspace\RepoOnly");
        fileSystem.Directory.SetCurrentDirectory(@"C:\workspace");
        fileSystem.File.WriteAllText(@"C:\workspace\RepoOnly\adr.config.json", """
        { "path": "docs\\adr", "templates": "docs\\adr-templates", "projectName": "RepoOnly" }
        """);

        var settings = new AdrSettings(fileSystem);
        var result = settings.TrySetContext("NoSuchProject");

        Assert.False(result.Success);
        Assert.Empty(result.Candidates);
        Assert.NotNull(result.ErrorMessage);
    }
}
