using Adr.Cli.Exceptions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Adr.Cli.Extensions;

/// <summary>
/// Helper methods for <see cref="AdrRecord" /> objects.
/// </summary>
public static class AdrRecordExtensions
{
    private const int MinimumTitleLength = 10;

    /// <summary>
    /// Format the ADR as a string.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <returns>
    /// One line of text
    /// </returns>
    public static string FormatString(this AdrRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyyMMdd} {record.Status.ToString() ?? "",-10} {record.Title.PadRight(80)[..80]}";
    }

    public static string FormatString(this TaskRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyyMMdd} {record.Status.ToString() ?? "",-10} {record.Title.PadRight(80)[..80]}";
    }

    /// <summary>
    /// Get the metadata for an <see cref="AdrRecord" /> as a stringbuilder with the json serialized
    /// metadata of an AdrRecord.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <param name="settings">
    /// Formatting options for the metadata
    /// </param>
    /// <returns>
    /// </returns>
    public static StringBuilder GetMetadata<T>(this T record, JsonSerializerOptions settings)
    {
        var data = JsonSerializer.Serialize(record, settings);
        return new StringBuilder(data);
    }

    /// <summary>
    /// Launch the default editor for markdown with the AdrRecord file as starup parameter.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <param name="settings">
    /// The <see cref="IAdrSettings" /> service is used to locate the file for the <see cref="AdrRecord" />.
    /// </param>
    /// <returns>
    /// The same record as provided as parameter.
    /// </returns>
    /// <exception cref="AdrException">
    /// </exception>
    public static AdrRecord LaunchEditor(this AdrRecord record, IAdrSettings settings, IProcessHelper process)
    {
        var fileInfo = settings.GetContentFile(DocumentType.Adr, record.FileName);
        var fullName = fileInfo.FullName;
        OpenPreferredEditor(process, fileInfo, fullName);
        return record;
    }

    public static TaskRecord LaunchEditor(this TaskRecord record, IAdrSettings settings, IProcessHelper process)
    {
        var fileInfo = settings.GetContentFile(DocumentType.Task, record.FileName);
        var fullName = fileInfo.FullName;
        OpenPreferredEditor(process, fileInfo, fullName);
        return record;
    }

    private static void OpenPreferredEditor(IProcessHelper process, System.IO.Abstractions.IFileInfo fileInfo, string fullName)
    {
        if (!fileInfo.Exists)
        {
            throw new AdrException($"Could not locate {fullName}.");
        }
        try
        {
            process.Start(fullName);
        }
        catch (Exception e)
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var url = fullName.Replace("&", "^&");
                process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.Start("xdg-open", fullName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                process.Start("open", fullName);
            }
            else
            {
                throw new AdrException($"Could not start an editor for {fullName}.", e);
            }
        }
    }

    /// <summary>
    /// Update the referenes in the metadata with a new target
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <param name="targetId">
    /// a target record identification.
    /// </param>
    /// <param name="remark">
    /// a short remark
    /// </param>
    public static AdrRecord UpdateReferenceRemark(this AdrRecord record, int targetId, string remark)
    {
        if (record.References.TryGetValue(targetId, out var currentRemark))
        {
            var newRemark = new List<string>();
            if (!string.IsNullOrEmpty(currentRemark))
            {
                newRemark.AddRange(currentRemark.Split(';', StringSplitOptions.RemoveEmptyEntries));
            }
            newRemark.Add(remark);
            record.References[targetId] = string.Join(';', newRemark.Distinct());
        }
        else
        {
            record.References.Add(targetId, remark);
        }
        return record;
    }

    /// <summary>
    /// Prepare the AdrRecord metadata so it can be saved with valid metadata.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <returns>
    /// The same record as provided as parameter, or a new record if the entry parameter was null.
    /// </returns>
    public static T PrepareForStorage<T>(this T record) where T : AdrRecordBase
    {
        ArgumentNullException.ThrowIfNull(record);
        record.FileName = $"{record.RecordId:D5}-{SanitizeFileName(record.Title)}";
        return record;
    }

    /// <summary>
    /// Add a text part at the end of the markdown element with the provided name.
    /// </summary>
    /// <param name="lines">
    /// The markdown content.
    /// </param>
    /// <param name="mdElement">
    /// The paragraph where the text should be appended.
    /// </param>
    /// <param name="newTextPart">
    /// The new text part.
    /// </param>
    /// <returns>
    /// </returns>
    public static IEnumerable<string> AddTextAtMdElement(this string[] lines, string mdElement, string newTextPart)
    {
        if (string.IsNullOrEmpty(mdElement) || string.IsNullOrEmpty(newTextPart))
        {
            foreach (var line in lines)
            {
                yield return line;
            }
        }
        else
        {
            var marker = $"## {mdElement}";
            var inTextBlock = false;
            var placedText = false;
            foreach (var line in lines)
            {
                if (inTextBlock && line.StartsWith("## ", StringComparison.Ordinal))
                {
                    yield return newTextPart;
                    inTextBlock = false;
                    placedText = true;
                }
                if (line.Equals(marker, StringComparison.OrdinalIgnoreCase))
                {
                    inTextBlock = true;
                }
                yield return line;
            }
            if (!placedText)
            {
                // add the part at the end
                yield return string.Empty;
                yield return marker;
                yield return string.Empty;
                yield return newTextPart;
                yield return string.Empty;
            }
        }
    }

    /// <summary>
    /// Replace the content for a markdown element with new content.
    /// </summary>
    /// <param name="lines">
    /// The markdown content.
    /// </param>
    /// <param name="mdElement">
    /// The paragraph where the text should be appended.
    /// </param>
    /// <param name="newTextPart">
    /// The new text part.
    /// </param>
    /// <returns>
    /// </returns>
    public static IEnumerable<string> ReplaceMdContent(this string[] lines, string mdElement, string[] newContent)
    {
        if (string.IsNullOrEmpty(mdElement))
        {
            foreach (var line in lines)
            {
                yield return line;
            }
        }
        else
        {
            var marker = $"## {mdElement}";
            var inTextBlock = false;
            foreach (var line in lines)
            {
                if (inTextBlock && line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inTextBlock = false;
                }
                if (line.Equals(marker, StringComparison.OrdinalIgnoreCase))
                {
                    inTextBlock = true;
                    yield return line;
                    yield return string.Empty;
                    foreach (var s in newContent)
                    {
                        yield return s;
                    }
                    if (newContent.Length > 0)
                    {
                        yield return string.Empty;
                    }
                }
                else
                {
                    if (!inTextBlock)
                    {
                        yield return line;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Remove all line where 'match' can be found in the markdown element
    /// </summary>
    /// <param name="lines">
    /// The markdown content.
    /// </param>
    /// <param name="mdElement">
    /// The paragraph where the text should be appended.
    /// </param>
    /// <param name="match">
    /// The partial match.
    /// </param>
    /// <returns>
    /// </returns>
    public static IEnumerable<string> RemoveFromMdElement(this string[] lines, string mdElement, string match)
    {
        if (string.IsNullOrEmpty(mdElement) || string.IsNullOrEmpty(match))
        {
            foreach (var line in lines)
            {
                yield return line;
            }
        }
        else
        {
            var marker = $"## {mdElement}";
            var inTextBlock = false;
            var previousLineWasEmpty = false;
            foreach (var line in lines)
            {
                if (inTextBlock && line.Contains(match, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (inTextBlock && line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inTextBlock = false;
                }
                if (line.Equals(marker, StringComparison.OrdinalIgnoreCase))
                {
                    inTextBlock = true;
                }

                // prevent double empty lines
                if (line.Length == 0)
                {
                    if (!previousLineWasEmpty)
                    {
                        yield return line;
                        previousLineWasEmpty = true;
                    }
                }
                else
                {
                    previousLineWasEmpty = false;
                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// Update the <see cref="AdrRecord" /> data using the markdown text content. The RecordId and
    /// Date in the <see cref="AdrRecord" /> are not modified.
    /// </summary>
    /// <param name="record">
    /// </param>
    /// <param name="lines">
    /// The file content
    /// </param>
    /// <returns>
    /// </returns>
    public static AdrRecord UpdateFromMarkdown(this AdrRecord record, int recordId, string[] lines, out bool metadataMmodified)
    {
        metadataMmodified = false;
        if (lines.Length <= 0)
        {
            return record;
        }

        if (recordId > 0 && recordId != record.RecordId)
        {
            metadataMmodified = true;
            record.RecordId = recordId;
        }

        // A title should contain at leat 10 characters
        string title;
        if (lines[0].Length < 10)
        {
            title = string.Empty;
        }
        else
        {
            title = lines[0][9..];
        }

        if (title.Length >= MinimumTitleLength && record.Title != title)
        {
            metadataMmodified = true;
            record.Title = title;
        }

        if (lines.TryFindMdElement("status", out var statusText))
        {
            var statusLine = string.Empty;
            foreach (var s in statusText)
            {
                var line = s.Trim();
                if (line.StartsWith("__") && line.EndsWith("__"))
                {
                    statusLine = s[2..^2];
                }
            }
            if (!string.IsNullOrEmpty(statusLine)
                && Enum.TryParse<AdrStatus>(statusLine, out var adrStatus)
                && record.Status != adrStatus)
            {
                metadataMmodified = true;
                record.Status = adrStatus;
            }
        }

        if (lines.TryFindMdElement("Context", out var context))
        {
            var sb = new StringBuilder();
            foreach (var s in context)
            {
                if (string.IsNullOrEmpty(s))
                {
                    break;
                }

                sb.Append(s.Trim());
                sb.Append(' ');
            }
            var metaContext = sb.ToString().Trim();
            if (record.Context != metaContext)
            {
                metadataMmodified = true;
                record.Context = metaContext;
            }
        }

        // Decision and consequences are not part of the metadata
        if (lines.TryFindMdElement("Decision", out var decision))
        {
            record.Decision = string.Join(Environment.NewLine, decision);
        }

        if (lines.TryFindMdElement("Consequences", out var consequences))
        {
            record.Consequences = string.Join(Environment.NewLine, consequences);
        }

        return record;
    }

    /// <summary>
    /// Validate critical elements is the <see cref="AdrRecord" />.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    public static void Validate<T>(this T record) where T : AdrRecordBase
    {
        if (record.RecordId < 0)
        {
            throw new AdrException("Record id must be a positive value");
        }

        if (string.IsNullOrEmpty(record.Title))
        {
            throw new AdrException("Title cannot be empty");
        }
    }

    /// <summary>
    /// Format the ADR as a string with detailed information.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <returns>
    /// </returns>
    public static string VerboseString(this AdrRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyy-MMM-dd} Status: {record.Status}" + Environment.NewLine
            + $"Title:   {record.Title}" + Environment.NewLine
            + $"Context: {record.Context}" + Environment.NewLine
            + "---";
    }

    /// <summary>
    /// Format the ADR as a string with detailed information.
    /// </summary>
    /// <param name="record">
    /// The AdrRecord.
    /// </param>
    /// <returns>
    /// </returns>
    public static string VerboseString(this TaskRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyy-MMM-dd} Status: {record.Status}" + Environment.NewLine
            + $"Title:   {record.Title}" + Environment.NewLine
            + $"Description: {record.Description}" + Environment.NewLine
            + "---";
    }

    /// <summary>
    /// Find the line with the header
    /// </summary>
    /// <param name="lines">
    /// Lines from a markdown text.
    /// </param>
    /// <param name="header">
    /// The header
    /// </param>
    /// <returns>
    /// </returns>
    private static int FindLineWithHeader(this string[] lines, string header)
    {
        var n = 0;
        var mdHeader = $"# {header}";
        while (n < lines.Length)
        {
            if (lines[n].Contains(mdHeader, StringComparison.OrdinalIgnoreCase))
            {
                return n;
            }

            n++;
        }
        return -1;
    }

    /// <summary>
    /// Remove illegal characters from a file name. The filename should not contain path
    /// information. The extension on the filename is allowed.
    /// </summary>
    /// <param name="fileName">
    /// </param>
    /// <returns>
    /// </returns>
    public static string SanitizeFileName(this string fileName)
    {
        // Maximum filename length on most systems is 255 characters
        // Reserve space for: "00000-" (6 chars) + ".json" (5 chars) = 11 chars
        // This leaves 244 characters for the sanitized title
        // This is too long, so clipp at 100 characters
        const int maxFileNameLength = 100;

        var sanitized = fileName
            .Replace(' ', '-')
            .ToLower();

        // Titles may contain characters that are invalid in a file name on this (or another)
        // platform - e.g. "/" in "success/failure" - replace them rather than let file creation fail.
        foreach (var invalidChar in Path.GetInvalidFileNameChars().Union(['/', '\\']))
        {
            sanitized = sanitized.Replace(invalidChar, '-');
        }

        // Truncate if too long
        if (sanitized.Length > maxFileNameLength)
        {
            sanitized = sanitized.Substring(0, maxFileNameLength);
        }

        return sanitized;
    }

    /// <summary>
    /// Find the text in between markdown tags.
    /// </summary>
    /// <param name="lines">
    /// </param>
    /// <param name="header">
    /// </param>
    /// <param name="element">
    /// </param>
    /// <returns>
    /// </returns>
    private static bool TryFindMdElement(this string[] lines, string header, out string[] element)
    {
        try
        {
            element = Array.Empty<string>();
            var n = FindLineWithHeader(lines, header);
            if (n < 0)
            {
                return false;
            }

            var sb = new List<string>();
            // skip first line after header
            n++;
            while (n < lines.Length - 1)
            {
                var text = lines[++n].Trim();
                if (text.StartsWith("## ", StringComparison.Ordinal))
                {
                    break;
                }

                sb.Add(text);
            }
            element = sb.ToArray();
            return true;
        }
        catch (Exception e)
        {
            element = new string[] { e.Message };
            return false;
        }
    }
}