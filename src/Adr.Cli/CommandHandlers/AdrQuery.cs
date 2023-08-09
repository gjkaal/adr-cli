using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

public class AdrQuery : IAdrQuery
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrNew> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IStdOut stdOut;

    public AdrQuery(
        IAdrSettings settings,
        ILogger<AdrNew> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut
    )
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
    }

    public async Task<int> ListAdrAsync(bool sortReverse, bool verbose)
    {
        logger.LogDebug($"List ADR {(sortReverse ? "newest first" : "oldest first")}");

        var listMeta = new Dictionary<int, string>();
        var idList = FindRecordIds(0);

        var items = idList.Distinct();
        if (sortReverse) items = items.Reverse();
        foreach (var recordId in items)
        {
            var adr = await adrRecordRepository.ReadMetadataAsync(recordId);
            if (adr == null) continue;
            var information = verbose
                ? adr.VerboseString()
                : adr.FormatString();
            listMeta.Add(adr.RecordId, information);
        }

        foreach (var recordId in listMeta.Keys)
        {
            stdOut.WriteLine(listMeta[recordId]);
        }
        return 0;
    }

    public async Task<int> FindAdrAsync(string filter, bool sortReverse, bool verbose, bool includeContent)
    {
        logger.LogDebug($"Find ADR containing '{filter}' {(sortReverse ? "newest first" : "oldest first")}");

        var listMeta = new Dictionary<int, string>();
        var idList = FindRecordIds(0);
        var words = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            stdOut.WriteLine("No filter provided");
        }

        var items = idList.Distinct();
        if (sortReverse) items = items.Reverse();
        foreach (var recordId in items)
        {
            var showRecord = false;
            var adr = await adrRecordRepository.ReadMetadataAsync(recordId);
            if (adr == null) continue;

            foreach (var word in words)
            {
                if (adr.Title.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    showRecord = true;
                    break;
                }
                if (!showRecord && adr.Context.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    showRecord = true;
                    break;
                }
                if (!showRecord && includeContent)
                {
                    var content = await adrRecordRepository.ReadContentAsync(recordId);
                    if (content.Any(m => m.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    {
                        showRecord = true;
                        break;
                    }
                }
            }

            if (showRecord)
            {
                var information = verbose
                    ? adr.VerboseString()
                    : adr.FormatString();
                listMeta.Add(adr.RecordId, information);
            }
        }

        foreach (var recordId in listMeta.Keys)
        {
            stdOut.WriteLine(listMeta[recordId]);
        }
        return 0;
    }

    private List<int> FindRecordIds(int startFromRecord)
    {
        var dir = settings.DocFolderInfo();
        var metadataFiles = dir.EnumerateFiles("*.md").Select(m => m.Name);
        var idList = new List<int>();
        foreach (var metadataFile in metadataFiles)
        {
            var parts = metadataFile.Split('-');
            if (parts.Length < 1) continue;
            if (int.TryParse(parts[0], out var recordId) && recordId >= startFromRecord)
                idList.Add(recordId);
        }

        return idList;
    }
}