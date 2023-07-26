using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;

namespace adr.Extensions;

/// <summary>
/// Helper methods for <see cref="AdrRecord"/> objects.
/// </summary>
public static class AdrRecordExtensions
{

    /// <summary>
    /// Format the ADR as a string of 80 characters
    /// </summary>
    /// <param name="record"></param>
    /// <returns></returns>
    public static string FormatString(this AdrRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyyMMdd} {record.Status.ToString()??"",-10} {record.Title.PadRight(80)[..80]}";
    }

    /// <summary>
    /// Get the metadata for an <see cref="AdrRecord"/> as a stringbuilder 
    /// with the json serialized metadata of an AdrRecord.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    public static StringBuilder GetMetadata(this AdrRecord record)
    {
        var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
        return record.GetMetadata(settings);
    }

    /// <summary>
    /// Get the metadata for an <see cref="AdrRecord"/> as a stringbuilder 
    /// with the json serialized metadata of an AdrRecord.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    /// <param name="settings">Formatting options for the metadata</param>
    /// <returns></returns>
    public static StringBuilder GetMetadata(this AdrRecord record, JsonSerializerSettings settings)
    {
        var data = JsonConvert.SerializeObject(record, settings);
        return new StringBuilder(data);
    }

    /// <summary>
    /// Launch the default editor for markdown with the AdrRecord file as starup parameter.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    /// <param name="settings">
    /// The <see cref="IAdrSettings"/> service is used to locate the 
    /// file for the <see cref="AdrRecord"/>.</param>
    /// <returns>The same record as provided as parameter.</returns>
    /// <exception cref="AdrException"></exception>
    public static AdrRecord LaunchEditor(this AdrRecord record, IAdrSettings settings)
    {
        var fileInfo = settings.GetContentFile(record.FileName);
        if (!fileInfo.Exists)
        {
            throw new AdrException($"Could not locate {fileInfo.FullName}.");
        }
        try
        {
            Process.Start(fileInfo.FullName);
        }
        catch (Exception e)
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var url = fileInfo.FullName.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", fileInfo.FullName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", fileInfo.FullName);
            }
            else
            {
                throw new AdrException($"Could start an editor for {fileInfo.FullName}.", e);
            }
        }
        return record;
    }

    /// <summary>
    /// Prepare the AdrRecord metadata so it can be saved with valid metadata.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    /// <returns>
    /// The same record as provided as parameter, 
    /// or a new record if the entry parameter was null.
    /// </returns>
    public static AdrRecord PrepareForStorage(this AdrRecord record)
    {
        if (record == null) record = new AdrRecord();
        record.FileName = $"{record.RecordId:D5}-{SanitizeFileName(record.Title)}";
        return record;
    }

    /// <summary>
    /// Validate critical elements is the <see cref="AdrRecord"/>.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    public static void Validate(this AdrRecord record)
    {
        if (record.RecordId < 0) throw new AdrException("Record id must be a positive value");
        if (string.IsNullOrEmpty(record.Title)) throw new AdrException("Title cannot be empty");
    }

    private static string SanitizeFileName(string title)
    {
        return title
            .Replace(' ', '-')
            .ToLower();
    }
}