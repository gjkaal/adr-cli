﻿using System.IO.Abstractions;

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
    /// <returns></returns>
    IFileInfo GetTemplate(string templateType);

    /// <summary>
    /// A boolean indicating that the ADR repository is initialized. 
    /// A new initialization should be blocked.
    /// </summary>
    /// <returns></returns>
    bool RepositoryInitialized();

    /// <summary>
    /// Save the current settings in a conmfiguration file
    /// </summary>
    /// <returns>The current IAdrSettings settings</returns>
    IAdrSettings Write();
}