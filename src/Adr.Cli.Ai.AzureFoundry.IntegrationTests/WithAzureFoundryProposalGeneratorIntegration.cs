using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Adr.Cli.Ai.AzureFoundry;

/// <summary>
/// Exercises the real Azure AI Foundry agent using the "ai" section from this repo's own
/// src/adr.config.json. Requires `az login` and a reachable, correctly configured Foundry
/// deployment (see AI-Setup.md) - this hits live Azure resources, unlike
/// Adr.Cli.Ai.AzureFoundry.UnitTests, and is meant to be run deliberately rather than as part of a
/// fast inner-loop test pass.
/// </summary>
public sealed class WithAzureFoundryProposalGeneratorIntegration
{
    [Fact]
    public async Task GenerateAsync_AgainstConfiguredFoundryAgent_ReturnsADraftedDecision()
    {
        var aiSettings = LoadAiSettingsFromRepoConfig();
        var generator = new AzureFoundryProposalGenerator(new FixedAiSettings(aiSettings), NullLogger<AzureFoundryProposalGenerator>.Instance);

        var existingRecords = new List<AdrSummary>
        {
            new()
            {
                RecordId = 1,
                Title = "We need ADR's to document architectural decisions",
                Status = AdrStatus.Accepted,
                Context = "Track and communicate significant architecture decisions over time."
            }
        };

        var result = await generator.GenerateAsync(
            "Use a message bus for service integration",
            "Services currently call each other synchronously over HTTP, which couples their deployments.",
            existingRecords);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Value);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.Decision));
    }

    private static AiProviderSettings LoadAiSettingsFromRepoConfig()
    {
        var configPath = FindRepoConfigFile();
        using var stream = File.OpenRead(configPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("ai", out var aiElement))
        {
            throw new InvalidOperationException(
                $"No \"ai\" section in {configPath}. Configure it per AI-Setup.md before running this integration test.");
        }

        return new AiProviderSettings
        {
            Provider = GetStringOrEmpty(aiElement, "provider"),
            Endpoint = GetStringOrEmpty(aiElement, "endpoint"),
            DeploymentName = GetStringOrEmpty(aiElement, "deploymentName")
        };
    }

    private static string GetStringOrEmpty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static string FindRepoConfigFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "adr.config.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find src/adr.config.json by walking up from the test output directory.");
    }

    /// <summary>
    /// Minimal IAdrSettings stub - AzureFoundryProposalGenerator only ever reads AiSettings, so
    /// every other member is unused and left throwing.
    /// </summary>
    private sealed class FixedAiSettings : IAdrSettings
    {
        public FixedAiSettings(AiProviderSettings aiSettings)
        {
            AiSettings = aiSettings;
        }

        public AiProviderSettings AiSettings { get; }

        public string CurrentPath => throw new NotSupportedException();
        public string DefaultDocFolder => throw new NotSupportedException();
        public string DefaultTasksFolder => throw new NotSupportedException();
        public string DefaultTemplates => throw new NotSupportedException();
        public string DocFolder { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public string TasksFolder { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public string TemplateFolder { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public string ProjectName => throw new NotSupportedException();
        public AdrContextInfo CurrentContext => throw new NotSupportedException();

        public AdrContextInfo TrySetContext(string workingDirectory) => throw new NotSupportedException();
        public IDirectoryInfo DocFolderInfo() => throw new NotSupportedException();
        public IDirectoryInfo TasksFolderInfo() => throw new NotSupportedException();
        public IDirectoryInfo TemplateFolderInfo() => throw new NotSupportedException();
        public IDirectoryInfo RootFolderInfo() => throw new NotSupportedException();
        public IFileInfo GetContentFile(DocumentType documentType, string fileName) => throw new NotSupportedException();
        public IFileInfo GetMetaFile(DocumentType documentType, string fileName) => throw new NotSupportedException();
        public int GetNextFileNumber(IDirectoryInfo directoryInfo) => throw new NotSupportedException();
        public IFileInfo GetTemplate(string templateType) => throw new NotSupportedException();
        public bool RepositoryInitialized() => throw new NotSupportedException();
        public bool TasksInitialized() => throw new NotSupportedException();
        public IAdrSettings Write() => throw new NotSupportedException();
        public IFileInfo GetDocumentFile(string fileName) => throw new NotSupportedException();
    }
}
