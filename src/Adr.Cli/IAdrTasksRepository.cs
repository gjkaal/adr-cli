using System.Text;
using System.Threading.Tasks;

namespace Adr.Cli;

public interface IAdrTasksRepository
{
    /// <summary>
    /// Get the layout template and use the record to create the content for a document.
    /// </summary>
    /// <param name="record">
    /// A task record with document information.
    /// </param>
    /// <returns>
    /// A stringbuilder containing the text for the document.
    /// </returns>
    Task<StringBuilder> GetLayoutAsync(TaskRecord record);

    /// <summary> Try to locate the metadata for a a task and return it in the <see
    /// cref="TaskRecord/> class. </summary> <param name="recordId">A zero of positive number
    /// integer.</param> <returns>The metadata for an ADR</returns>
    Task<TaskRecord?> ReadMetadataAsync(int recordId);

    /// <summary>
    /// Try to locate the text content for a task and return it in a set of lines.
    /// </summary>
    /// <param name="recordId">
    /// A zero of positive number integer.
    /// </param>
    /// <returns>
    /// The document content.
    /// </returns>
    Task<string[]> ReadContentAsync(int recordId);

    /// <summary>
    /// Write the metadata in a text based data file.
    /// </summary>
    /// <param name="record">
    /// The metadata for a task.
    /// </param>
    /// <returns>
    /// zero for success, not zero for failure.
    /// </returns>
    Task<int> WriteRecordAsync(TaskRecord record);

    /// <summary>
    /// Try to locate the text content for an ADR and update the AdrRecord with textual information.
    /// </summary>
    /// <param name="recordId">
    /// A zero of positive number integer.
    /// </param>
    /// <param name="record">
    /// The metadata for an ADR.
    /// </param>
    /// <returns>
    /// zero for success, not zero for failure.
    /// </returns>
    Task<int> UpdateMetadataAsync(int recordId, TaskRecord record);

    /// <summary>
    /// Update the text content file using additional information in the metadata.
    /// </summary>
    /// <param name="record">
    /// The metadata for an ADR.
    /// </param>
    /// <param name="lines">
    /// The document content
    /// </param>
    /// <returns>
    /// zero for success, not zero for failure.
    /// </returns>
    Task<int> UpdateContentAsync(TaskRecord record, string[] lines);

    /// <summary>
    /// Define and fill a document in the project root folder.
    /// </summary>
    /// <param name="fileName">
    /// The file name used to create or update a document file.
    /// </param>
    /// <param name="fileContent">
    /// The content for the file.
    /// </param>
    /// <returns>
    /// Full path to the generated file.
    /// </returns>
    Task<(bool success, string fullFilePath)> CreateRootDocumentAsync(string fileName, StringBuilder fileContent);
}