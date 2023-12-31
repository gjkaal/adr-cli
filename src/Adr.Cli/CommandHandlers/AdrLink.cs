﻿using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

public class AdrLink : IAdrLink
{
    private readonly ILogger<AdrLink> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IStdOut stdOut;

    public AdrLink(
        ILogger<AdrLink> logger,
        IAdrRecordRepository adrRecordRepository,
        IStdOut stdOut)
    {
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.stdOut = stdOut;
    }

    public Task<int> HandleLinkAdrAsync(string sourceId, string targetId, string reason, AdrLinkTypeOperation operation)
    {
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

        return HandleLinkAdrAsync(linkId, targetLinkId, reason, operation);
    }

    public Task<int> HandleLinkAdrAsync(int sourceId, int targetId, string reason, AdrLinkTypeOperation operation)
    {
        if (string.IsNullOrEmpty(reason)) reason = "Extends";
        return (operation == AdrLinkTypeOperation.Create)
        ? LinkAdrAsync(sourceId, targetId, reason)
        : RemoveLinkAsync(sourceId, targetId);
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
        if (targetContent == null || targetContent.Length == 0)
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