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
}
