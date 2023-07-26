using adr.Extensions;
using adr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;

namespace adr.CommandHandlers;

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

    public static IEnumerable<Command> CommandHandler(IServiceProvider serviceProvider)
    {
        return new[] { 
            InitListCommand(serviceProvider),
            InitQueryCommand(serviceProvider)
        };
    }

    private static readonly Option<bool> sortReverse = new("--desc", "Show the ADR's with the latest ADR first")
    {
        IsRequired = false,        
    };

    private static readonly Option<bool> includeContent = new("--full", "Search the full records (slow)")
    {
        IsRequired = false,
    };

    private static readonly Option<string> filter = new("--find", "Only show an ADR if a word or words in used the ADR")
    {
        IsRequired = true,
    };

    private static Command InitListCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("list", "List all Architecture Decision Records");

        cmd.AddOption(sortReverse);
        cmd.SetHandler(async (sortReverse) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrQuery>();
            await c.ListAdrAsync(sortReverse);
        }, sortReverse);

        return cmd;
    }

    private static Command InitQueryCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("find", "Find Architecture Decision Records");

        cmd.AddOption(sortReverse);
        cmd.AddOption(includeContent);
        cmd.AddOption(filter);
        cmd.SetHandler(async (filter, sortReverse, includeContent) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrQuery>();
            await c.FindAdrAsync(filter, sortReverse, includeContent);
        }, filter, sortReverse, includeContent);

        return cmd;
    }

    public async Task<int> ListAdrAsync(bool sortReverse) {
        logger.LogDebug($"List ADR {(sortReverse ? "newest first" : "oldest first")}");

        var listMeta = new Dictionary<int, string>();
        var dir = settings.DocFolderInfo();
        var metadataFiles = dir.EnumerateFiles("*.md").Select(m => m.Name);
        var idList = new List<int>();
        foreach ( var metadataFile in metadataFiles )
        {
            var parts = metadataFile.Split('-');
            if (parts.Length < 1) continue;
            if (int.TryParse(parts[0], out var recordId))
            {
                idList.Add(recordId);
            }
        }

        foreach (var recordId in idList.Distinct())
        {
            var adr = await adrRecordRepository.ReadMetadataAsync(recordId);
            if (adr == null) continue;
            listMeta.Add(adr.RecordId, adr.FormatString());
        }

        foreach(var recordId in listMeta.Keys)
        {
            stdOut.WriteLine(listMeta[recordId]);
        }
        return 0;
    }

    public Task<int> FindAdrAsync(string filter, bool sortReverse, bool includeContent)
    {
        logger.LogDebug($"Find ADR containing '{filter}' {(sortReverse ? "newest first" : "oldest first")}");
        return Task.FromResult(0);
    }
}

