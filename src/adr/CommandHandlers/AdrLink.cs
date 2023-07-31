using adr.Extensions;
using adr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;

namespace adr.CommandHandlers;

public class AdrLink : IAdrLink
{
    private readonly IAdrSettings settings;
    private readonly ILogger<AdrLink> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IStdOut stdOut;
    private readonly IProcessHelper processHelper;

    public AdrLink(
        IAdrSettings settings,
        ILogger<AdrLink> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut,
        IProcessHelper processHelper)
    {
        this.settings = settings;
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
        this.processHelper = processHelper;
    }

    public static IEnumerable<Command> CommandHandler(IServiceProvider serviceProvider)
    {
        return new[] { 
            InitLinkCommand(serviceProvider),
            InitUnLinkCommand(serviceProvider) 
        };
    }

    private static readonly Option<string> sourceId = new("--source", "The source ADR" )
    {
        IsRequired = true,
    };

    private static readonly Option<string> reason = new("--reason", "The reason for the link.")
    {
        IsRequired = false
    };

    private static readonly Option<string> targetId = new("--target", "The target ADR")
    {
        IsRequired = true
    };

    private static Command InitLinkCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("link", "Link 2 ADR's for ammend / clarify or some other reason");
        sourceId.AddAlias("-s");
        targetId.AddAlias("-t");
        reason.AddAlias("-r");

        cmd.AddOption(sourceId);
        cmd.AddOption(targetId);
        cmd.AddOption(reason);

        cmd.SetHandler(async (sourceId, targetId, reason) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrLink>();
            await c.HandleLinkAdrAsync(sourceId, targetId, reason, AdrLinkTypeOperation.Create);
        }, sourceId, targetId, reason);
        return cmd;
    }

    private static Command InitUnLinkCommand(IServiceProvider serviceProvider)
    {
        var cmd = new Command("rlink", "Remove all links from one ADR to another");
        sourceId.AddAlias("-s");
        targetId.AddAlias("-t");

        cmd.AddOption(sourceId);
        cmd.AddOption(targetId);

        cmd.SetHandler(async (sourceId, targetId) =>
        {
            var c = serviceProvider.GetRequiredService<IAdrLink>();
            await c.HandleLinkAdrAsync(sourceId, targetId, "remove", AdrLinkTypeOperation.Remove);
        }, sourceId, targetId);
        return cmd;
    }

    public Task<int> HandleLinkAdrAsync(string sourceId, string targetId, string reason, AdrLinkTypeOperation operation)
    {
        if (string.IsNullOrEmpty(reason)) reason = "Extends";
        if (!(int.TryParse(sourceId, out var linkId) && int.TryParse(targetId, out var targetLinkId)))
        {
            logger.LogError($"Could not interpret [source: {sourceId}] or [target: {targetId}] as a valid number");
            stdOut.WriteLine("Source id and target id should be valid identifiers.");
            stdOut.WriteLine("No link has been made.");
            return Task.FromResult(-1);
        }
        if (linkId <= 0 || targetLinkId <= 0)
        {
            logger.LogError($"Identifier not valid [source: {sourceId}] or [target: {targetId}].");
            stdOut.WriteLine("Source id and target id should be positive numbers.");
            stdOut.WriteLine("No link has been made.");
            return Task.FromResult(-1);
        }
        return (operation == AdrLinkTypeOperation.Create)
            ? LinkAdrAsync(linkId, targetLinkId, reason)
            : RemoveLinkAsync(linkId, targetLinkId);
    }

    public async Task<int> LinkAdrAsync(int sourceId, int targetId, string remark)
    {
        logger.LogInformation($"Creating link between {sourceId} and {targetId} for {remark}.");

        // Find content and metadata
        var sourceContent = await adrRecordRepository.ReadContentAsync(sourceId);
        if (sourceContent == null || sourceContent.Length == 0)
        {
            stdOut.WriteLine($"Source ADR does not exist: {sourceId:D5}.");
            return -1;
        }

        var targetContent = await adrRecordRepository.ReadContentAsync(targetId);
        if (targetContent == null || targetContent.Length==0)
        {
            stdOut.WriteLine($"Target ADR does not exist: {targetId:D5}.");
            return -1;
        }
        var sourceMeta = await adrRecordRepository.ReadMetadataAsync(sourceId);
        if (sourceMeta == null)
        {
            sourceMeta = new AdrRecord();
            sourceMeta.UpdateFromMarkdown(sourceId, sourceContent, out _);
        }
        var targetMeta = await adrRecordRepository.ReadMetadataAsync(targetId);
        if (targetMeta == null)
        {
            targetMeta = new AdrRecord();
            targetMeta.UpdateFromMarkdown(targetId, targetContent, out _);
        }

        var newMetadata = sourceMeta.UpdateReferenceRemark(targetId, remark);

        var linkText = $"{remark} [{targetId:D5}.{targetMeta.Title}](.\\{targetMeta.FileName}){Environment.NewLine}";

        var newContent = sourceContent.AddTextAtMdElement("Status", linkText).ToArray();

        await adrRecordRepository.UpdateMetadataAsync(sourceId, newMetadata);
        await adrRecordRepository.UpdateContentAsync(sourceMeta, newContent);

        return 0;
    }

   

    public async Task<int> RemoveLinkAsync(int sourceId, int targetId)
    {
        logger.LogInformation($"Removing all reference link from {sourceId} to {targetId}.");

        // Find content and metadata
        var sourceContent = await adrRecordRepository.ReadContentAsync(sourceId);
        if (sourceContent == null || sourceContent.Length == 0)
        {
            stdOut.WriteLine($"Source ADR does not exist: {sourceId:D5}.");
            return -1;
        }

        var sourceMeta = await adrRecordRepository.ReadMetadataAsync(sourceId);
        if (sourceMeta == null)
        {
            sourceMeta = new AdrRecord();
            sourceMeta.UpdateFromMarkdown(sourceId, sourceContent, out _);
        }

        sourceMeta.References.Remove(targetId);

        var linkText = $"[{targetId:D5}.";

        var newContent = sourceContent.RemoveFromMdElement("Status", linkText).ToArray();

        await adrRecordRepository.UpdateMetadataAsync(sourceId, sourceMeta);
        await adrRecordRepository.UpdateContentAsync(sourceMeta, newContent);

        return 0;
    }
}

