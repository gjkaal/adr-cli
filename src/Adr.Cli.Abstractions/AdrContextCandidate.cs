namespace Adr.Cli;

/// <summary>
/// One adr.config.json found while searching for a context to switch to - either an exact match or
/// one option among several found by <see cref="IAdrSettings.TrySetContext" />'s downward search.
/// </summary>
public class AdrContextCandidate
{
    public string ProjectName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
}
