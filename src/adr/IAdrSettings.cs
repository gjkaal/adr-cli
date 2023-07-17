using System.IO.Abstractions;

namespace adr;

public interface IAdrSettings
{
    /// <summary>
    /// Relative or full file location for the documentation folder.
    /// </summary>
    string DocFolder { get; set; }

    /// <summary>
    /// Relative or full file location for templates.
    /// </summary>
    string TemplateFolder { get; set; }

    /// <summary>
    /// Find a content file with the provided base name.
    /// </summary>
    /// <param name="fileName">Any string</param>
    /// <remarks>the filename will be sanitized.</remarks>
    /// <returns></returns>
    IFileInfo GetContentFile(string fileName);

    IFileInfo GetMetaFile(string fileName);
    int GetNextFileNumber();
    IFileInfo GetTemplate(string templateType);
    bool RepositoryInitialized();
    IAdrSettings Write();
}