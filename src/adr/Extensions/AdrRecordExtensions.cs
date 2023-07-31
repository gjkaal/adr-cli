using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace adr.Extensions;

/// <summary>
/// Helper methods for <see cref="AdrRecord"/> objects.
/// </summary>
public static class AdrRecordExtensions
{

    private const int MinimumTitleLength = 10;

    /// <summary>
    /// Format the ADR as a string.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    /// <returns>One line of text</returns>
    public static string FormatString(this AdrRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyyMMdd} {record.Status.ToString() ?? "",-10} {record.Title.PadRight(80)[..80]}";
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
    public static AdrRecord LaunchEditor(this AdrRecord record, IAdrSettings settings, IProcessHelper process)
    {
        var fileInfo = settings.GetContentFile(record.FileName);
        if (!fileInfo.Exists)
        {
            throw new AdrException($"Could not locate {fileInfo.FullName}.");
        }
        try
        {
            process.Start(fileInfo.FullName);
        }
        catch (Exception e)
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var url = fileInfo.FullName.Replace("&", "^&");
                process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.Start("xdg-open", fileInfo.FullName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                process.Start("open", fileInfo.FullName);
            }
            else
            {
                throw new AdrException($"Could start an editor for {fileInfo.FullName}.", e);
            }
        }
        return record;
    }

    /// <summary>
    /// Update the referenes in the metadata with a new target
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    /// <param name="targetId">a target record identification.</param>
    /// <param name="remark">a short remark</param>
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
    /// Add a text part at the end of the markdown element with the provided name.
    /// </summary>
    /// <param name="lines">The markdown content.</param>
    /// <param name="mdElement">The paragraph where the text should be appended.</param>
    /// <param name="newTextPart">The new text part.</param>
    /// <returns></returns>
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
            foreach (var line in lines)
            {
                if (inTextBlock && line.StartsWith("## ", StringComparison.Ordinal))
                {
                    yield return newTextPart;
                    inTextBlock = false;
                }
                if (line.Equals(marker, StringComparison.OrdinalIgnoreCase))
                {
                    inTextBlock = true;
                }
                yield return line;
            }
        }
    }

    /// <summary>
    /// Remove all line where 'match' can be found in the markdown element
    /// </summary>
    /// <param name="lines">The markdown content.</param>
    /// <param name="mdElement">The paragraph where the text should be appended.</param>
    /// <param name="match">The partial match.</param>
    /// <returns></returns>
    public static IEnumerable<string> RemoveFromMdElement(this string[] lines,  string mdElement, string match)
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
                yield return line;
            }
        }
    }

    /// <summary>
    /// Update the <see cref="AdrRecord"/> data using the markdown text content.
    /// The RecordId and Date in the <see cref="AdrRecord"/> are not modified.
    /// </summary>
    /// <param name="record"></param>
    /// <param name="lines">The file content</param>
    /// <returns></returns>
    public static AdrRecord UpdateFromMarkdown(this AdrRecord record, int recordId, string[] lines, out bool metadataMmodified)
    {
        metadataMmodified = false;
        if (lines.Length <= 0) return record;

        if (recordId > 0 && recordId != record.RecordId)
        {
            metadataMmodified = true;
            record.RecordId = recordId;
        }

        // A title should contain at leat 10 characters
        var title = lines[0].Split('.', StringSplitOptions.RemoveEmptyEntries).Last().Trim();
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
            foreach(var s in context)
            {
                if (string.IsNullOrEmpty(s)) break;
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
        if (lines.TryFindMdElement("Decision", out var decision)) record.Decision = string.Join(Environment.NewLine, decision);
        if (lines.TryFindMdElement("Consequences", out var consequences)) record.Consequences = string.Join(Environment.NewLine, consequences);

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

    /// <summary>
    /// Format the ADR as a string with detailed information.
    /// </summary>
    /// <param name="record">The AdrRecord.</param>
    /// <returns></returns>
    public static string VerboseString(this AdrRecord record)
    {
        return $"{record.RecordId:D5} {record.DateTime:yyyy-MMM-dd} Status: {record.Status}" + Environment.NewLine
            + $"Title:   {record.Title}" + Environment.NewLine
            + $"Context: {record.Context}" + Environment.NewLine
            + "---";
    }
    /// <summary>
    /// Find the line with the header
    /// </summary>
    /// <param name="lines">Lines from a markdown text.</param>
    /// <param name="header">The header</param>
    /// <returns></returns>
    private static int FindLineWithHeader(this string[] lines, string header)
    {
        var n = 0;
        var mdHeader = $"# {header}";
        while (n < lines.Length)
        {
            if (lines[n].Contains(mdHeader, StringComparison.OrdinalIgnoreCase)) return n;
            n++;
        }
        return -1;
    }

    private static string SanitizeFileName(string title)
    {
        return title
            .Replace(' ', '-')
            .ToLower();
    }

    /// <summary>
    /// Find the text in between markdown tags.
    /// </summary>
    /// <param name="lines"></param>
    /// <param name="header"></param>
    /// <param name="element"></param>
    /// <returns></returns>
    private static bool TryFindMdElement(this string[] lines, string header, out string[] element)
    {
        try
        {
            element = Array.Empty<string>();
            var n = FindLineWithHeader(lines, header);
            if (n < 0) return false;
            var sb = new List<string>();
            // skip first line after header
            n++;
            while (n < lines.Length - 1)
            {
                var text = lines[++n].Trim();
                if (text.StartsWith("## ", StringComparison.Ordinal)) break;
                sb.Add(text);
            }
            element = sb.ToArray();
            return true;
        }
        catch(Exception e) {
            element = new string[] { e.Message };
            return false; 
        }
    }
}