using System.IO.Abstractions;

namespace Adr.Cli;

public interface IAdrSettings
{
    /// <summary>
    /// The current folder location.
    /// </summary>
    string CurrentPath { get; }

    /// <summary>
    /// The default location for Adr records.
    /// </summary>
    string DefaultDocFolder { get; }

    /// <summary>
    /// The default location for Adr templates.
    /// </summary>
    string DefaultTemplates { get; }

    /// <summary>
    /// Relative or full file location for the documentation folder.
    /// </summary>
    string DocFolder { get; set; }

    /// <summary>
    /// Relative or full file location for templates.
    /// </summary>
    string TemplateFolder { get; set; }

    /// <summary>
    /// The name for the ADR project.
    /// </summary>
    string ProjectName { get; }

    /// <summary>
    /// Directory information for the ADR documents.
    /// </summary>
    IDirectoryInfo DocFolderInfo();

    /// <summary>
    /// Directory information for the ADR templates.
    /// </summary>
    IDirectoryInfo TemplateFolderInfo();

    /// <summary>
    /// Directory information for the folder where the settings file is located.
    /// </summary>
    IDirectoryInfo RootFolderInfo();

    /// <summary>
    /// Find a content file with the provided base name.
    /// </summary>
    /// <param name="fileName">Any string</param>
    /// <remarks>the filename will be sanitized.</remarks>
    /// <returns>a file information object that can be used to manage the content file.</returns>
    IFileInfo GetContentFile(string fileName);

    /// <summary>
    /// Find a metadata file with the provided base name.
    /// </summary>
    /// <param name="fileName">Any string</param>
    /// <remarks>the filename will be sanitized.</remarks>
    /// <returns>a file information object that can be used to manage the metadata file.</returns>
    IFileInfo GetMetaFile(string fileName);

    /// <summary>
    /// Find the next file identification, starting with 0 (zero) for an uninitialized ADR folder.
    /// </summary>
    /// <returns>A positive integer number.</returns>
    int GetNextFileNumber();

    /// <summary>
    /// Find the next file identification for a template.
    /// </summary>
    /// <param name="templateType"></param>
    /// <returns>A file information class (<see cref="IFileInfo"/>).</returns>
    IFileInfo GetTemplate(string templateType);

    /// <summary>
    /// A boolean indicating that the ADR repository is initialized. 
    /// A new initialization should be blocked.
    /// </summary>
    /// <returns>True if the repository is already initialized.</returns>
    bool RepositoryInitialized();

    /// <summary>
    /// Save the current settings in a conmfiguration file
    /// </summary>
    /// <returns>The current IAdrSettings settings</returns>
    IAdrSettings Write();

    /// <summary>
    /// Find a documentation file. Typically, a documentation file resides next to the config 
    /// file in the <see cref="CurrentPath"/> folder.
    /// </summary>
    /// <param name="fileName">A valid file name.</param>
    /// <returns>A file information class (<see cref="IFileInfo"/>) for managing additional project documents.</returns>
    IFileInfo GetDocumentFile(string fileName);
}