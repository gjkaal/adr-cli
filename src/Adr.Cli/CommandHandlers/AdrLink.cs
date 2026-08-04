using Adr.Cli.Extensions;
using Adr.Cli.Services;

using McpCore;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Adr.Cli.CommandHandlers;

public class AdrLink : IAdrLink
{
    private readonly ILogger<AdrLink> logger;
    private readonly IAdrRecordRepository adrRecordRepository;
    private readonly IAdrTasksRepository tasksRepository;
    private readonly IStdOut stdOut;

    // Per-record locking to prevent concurrent modifications to the same ADR
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> recordLocks = new();

    public AdrLink(
        ILogger<AdrLink> logger,
        IAdrRecordRepository adrRecordRepository,
        IAdrTasksRepository tasksRepository,
        IStdOut stdOut)
    {
        this.logger = logger;
        this.adrRecordRepository = adrRecordRepository;
        this.tasksRepository = tasksRepository;
        this.stdOut = stdOut;
    }

    /// <summary>
    /// Get or create a semaphore for a specific record ID to ensure atomic operations
    /// </summary>
    private static SemaphoreSlim GetRecordLock(int recordId)
    {
        return recordLocks.GetOrAdd(recordId, _ => new SemaphoreSlim(1, 1));
    }

    public Task<Response> HandleLinkAdrAsync(string sourceId, string targetId, string reason, AdrLinkTypeOperation operation)
    {
        if (!(int.TryParse(sourceId, out var linkId) && int.TryParse(targetId, out var targetLinkId)))
        {
            logger.LogError($"Could not interpret [source: {sourceId}] or [target: {targetId}] as a valid number");
            stdOut.WriteLine("Source id and target id should be valid identifiers.");
            stdOut.WriteLine("No link has been made.");
            return Task.FromResult(Response.Fail("Source id and target id should be valid identifiers. No link has been made."));
        }
        if (linkId <= 0 || targetLinkId <= 0)
        {
            logger.LogError($"Identifier not valid [source: {sourceId}] or [target: {targetId}].");
            stdOut.WriteLine("Source id and target id should be positive numbers.");
            stdOut.WriteLine("No link has been made.");
            return Task.FromResult(Response.Fail("Source id and target id should be positive numbers. No link has been made."));
        }

        return HandleLinkAdrAsync(linkId, targetLinkId, reason, operation);
    }

    public Task<Response> HandleLinkAdrAsync(int sourceId, int targetId, string reason, AdrLinkTypeOperation operation)
    {
        if (string.IsNullOrEmpty(reason))
        {
            reason = "Extends";
        }

        return (operation == AdrLinkTypeOperation.Create)
        ? LinkAdrAsync(sourceId, targetId, reason)
        : RemoveLinkAsync(sourceId, targetId);
    }

    public async Task<Response> LinkAdrAsync(int sourceId, int targetId, string remark)
    {
        logger.LogInformation($"Creating link between {sourceId} and {targetId} for {remark}.");

        // Acquire lock for the source record to prevent concurrent modifications
        var recordLock = GetRecordLock(sourceId);
        await recordLock.WaitAsync();

        try
        {
            // Find content and metadata
            var sourceContent = await adrRecordRepository.ReadContentAsync(sourceId);
            if (sourceContent == null || sourceContent.Length == 0)
            {
                stdOut.WriteLine($"Source ADR does not exist: {sourceId:D5}.");
                return Response.Fail($"Source ADR does not exist: {sourceId:D5}.");
            }

            var targetContent = await adrRecordRepository.ReadContentAsync(targetId);
            if (targetContent == null || targetContent.Length == 0)
            {
                stdOut.WriteLine($"Target ADR does not exist: {targetId:D5}.");
                return Response.Fail($"Target ADR does not exist: {sourceId:D5}.");
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

            // Ensure FileName is set before updating files
            // This is critical when metadata is reconstructed from markdown
            newMetadata.PrepareForStorage();

            var linkText = $"{remark} [{targetId:D5}.{targetMeta.Title}](.\\{targetMeta.FileName}){Environment.NewLine}";

            var newContent = sourceContent.AddTextAtMdElement("Status", linkText).ToArray();

            var metadataUpdateCount = await adrRecordRepository.UpdateMetadataAsync(sourceId, newMetadata);
            if (metadataUpdateCount < 0)
            {
                stdOut.WriteLine($"Could not update metadata for ADR {sourceId:D5} ({newMetadata.FileName}).");
                return Response.Fail($"Could not update metadata for ADR {sourceId:D5}. No link has been made.");
            }

            var contentUpdateCount = await adrRecordRepository.UpdateContentAsync(newMetadata, newContent);
            if (contentUpdateCount < 0)
            {
                stdOut.WriteLine($"Could not update content for ADR {sourceId:D5} ({newMetadata.FileName}).");
                return Response.Fail($"Could not update content for ADR {sourceId:D5}. The link metadata was saved but the markdown was not updated.");
            }

            return Response.Ok($"Created link between {sourceId} and {targetId} for {remark}.");
        }
        finally
        {
            // Always release the lock
            recordLock.Release();
        }
    }

    public async Task<Response> RemoveLinkAsync(int sourceId, int targetId)
    {
        logger.LogInformation($"Removing all reference link from {sourceId} to {targetId}.");

        // Acquire lock for the source record to prevent concurrent modifications
        var recordLock = GetRecordLock(sourceId);
        await recordLock.WaitAsync();

        try
        {
            // Find content and metadata
            var sourceContent = await adrRecordRepository.ReadContentAsync(sourceId);
            if (sourceContent == null || sourceContent.Length == 0)
            {
                stdOut.WriteLine($"Source ADR does not exist: {sourceId:D5}.");
                return Response.Fail($"Source ADR does not exist: {sourceId:D5}.");
            }

            var sourceMeta = await adrRecordRepository.ReadMetadataAsync(sourceId);
            if (sourceMeta == null)
            {
                sourceMeta = new AdrRecord();
                sourceMeta.UpdateFromMarkdown(sourceId, sourceContent, out _);
            }

            sourceMeta.References.Remove(targetId);

            // Ensure FileName is set before updating files
            // This is critical when metadata is reconstructed from markdown
            sourceMeta.PrepareForStorage();

            var linkText = $"[{targetId:D5}.";

            var newContent = sourceContent.RemoveFromMdElement("Status", linkText).ToArray();

            var metadataUpdateCount = await adrRecordRepository.UpdateMetadataAsync(sourceId, sourceMeta);
            if (metadataUpdateCount < 0)
            {
                stdOut.WriteLine($"Could not update metadata for ADR {sourceId:D5} ({sourceMeta.FileName}).");
                return Response.Fail($"Could not update metadata for ADR {sourceId:D5}. No link has been removed.");
            }

            var contentUpdateCount = await adrRecordRepository.UpdateContentAsync(sourceMeta, newContent);
            if (contentUpdateCount < 0)
            {
                stdOut.WriteLine($"Could not update content for ADR {sourceId:D5} ({sourceMeta.FileName}).");
                return Response.Fail($"Could not update content for ADR {sourceId:D5}. The link metadata was updated but the markdown was not updated.");
            }

            return Response.Ok($"Removed all reference link from {sourceId} to {targetId}.");
        }
        finally
        {
            // Always release the lock
            recordLock.Release();
        }
    }


    public async Task<Response> LinkAdrToTaskAsync(int adrId, int taskId, string remark)
    {
        logger.LogInformation($"Linking task {taskId} to ADR {adrId} for {remark}.");

        var adr = await adrRecordRepository.ReadMetadataAsync(adrId);
        if (adr == null)
        {
            return Response.Fail($"ADR does not exist: {adrId:D5}.");
        }

        var task = await tasksRepository.ReadMetadataAsync(taskId);
        if (task == null)
        {
            return Response.Fail($"Task does not exist: {taskId:D5}.");
        }

        if (adr.RelatedTasks.ContainsKey(taskId))
        {
            return Response.Fail($"ADR '{adr.Title}' is already related to task '{task.Title}'.");
        }

        adr.RelatedTasks[taskId] = task.Title;
        var updateCount = await adrRecordRepository.UpdateMetadataAsync(adrId, adr);

        return updateCount < 0
            ? Response.Fail($"Could not update ADR '{adr.Title}' with Id:{adrId}")
            : updateCount > 0 ? Response.Ok($"ADR '{adr.Title}' with Id:{adrId} is now related to task '{task.Title}' with Id:{taskId}")
            : Response.Fail($"ADR '{adr.Title}' with Id:{adrId} is not modified.");
    }

    public async Task<Response> RemoveAdrTaskLinkAsync(int adrId, int taskId)
    {
        logger.LogInformation($"Removing task {taskId} from ADR {adrId}'s related tasks.");

        var adr = await adrRecordRepository.ReadMetadataAsync(adrId);
        if (adr == null)
        {
            return Response.Fail($"ADR does not exist: {adrId:D5}.");
        }

        if (!adr.RelatedTasks.ContainsKey(taskId))
        {
            return Response.Fail($"ADR '{adr.Title}' is not related to a task with Id:{taskId}.");
        }
        adr.RelatedTasks.Remove(taskId);

        var updateCount = await adrRecordRepository.UpdateMetadataAsync(adrId, adr);
        return updateCount < 0
            ? Response.Fail($"Could not update ADR '{adr.Title}' with Id:{adrId}")
            : updateCount > 0 ? Response.Ok($"ADR '{adr.Title}' with Id:{adrId} is modified, the relation to task with Id:{taskId} is removed.")
            : Response.Fail($"ADR '{adr.Title}' with Id:{adrId} is not modified.");
    }
}