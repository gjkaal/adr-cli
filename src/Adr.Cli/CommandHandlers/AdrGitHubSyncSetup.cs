using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Text;

using Adr.Cli.Services;
using Adr.Cli.Sync;

using McpCore;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class AdrGitHubSyncSetup
{
    public static Command ExportAdrCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("adr-export", "Export ADRs to the currently configured sync provider as real GitHub Issues (see ADR 00010), creating or updating each ADR's Issue, pushing its status, and attaching each related task as a GitHub sub-issue.");

        var ids = new Option<string>("--id") { Description = "Comma or space separated ADR ids to export. Required unless --filter is given." };
        var filter = CommandOptions.Filter;
        var force = new Option<bool>("--force") { Description = "Skip the remote-divergence check and overwrite the external issue with local content regardless. Intended for a single ADR (--id) at a time." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Report what would be created/updated/mismatched, including status mapping and related-task attachment, without writing anything locally or remotely." };

        cmd.Options.Add(ids);
        cmd.Options.Add(filter);
        cmd.Options.Add(force);
        cmd.Options.Add(dryRun);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var idValues = ParseIds(ctx.GetValue(ids));
            var filterValue = ctx.GetValue(filter);
            var forceValue = ctx.GetValue(force);
            var dryRunValue = ctx.GetValue(dryRun);

            var c = serviceProvider.GetRequiredService<IAdrGitHubSync>();
            var result = await c.ExportAdrAsync(idValues, filterValue, forceValue, dryRunValue);
            stdOut.Write(FormatBatchResponse(result));
        });
        return cmd;
    }

    public static Command ImportAdrCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("adr-import", "Import ADR status (and, when safe, content) from the currently configured sync provider (see ADR 00010). Defaults to every ADR with a sync link for the active provider when neither --id nor --filter is given.");

        var ids = new Option<string>("--id") { Description = "Comma or space separated ADR ids to import." };
        var filter = CommandOptions.Filter;
        var dryRun = new Option<bool>("--dry-run") { Description = "Report what would be imported (status mapping, content pull/mismatch) without writing anything locally or remotely." };

        cmd.Options.Add(ids);
        cmd.Options.Add(filter);
        cmd.Options.Add(dryRun);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var idValues = ParseIds(ctx.GetValue(ids));
            var filterValue = ctx.GetValue(filter);
            var dryRunValue = ctx.GetValue(dryRun);

            var c = serviceProvider.GetRequiredService<IAdrGitHubSync>();
            var result = await c.ImportAdrStatusAsync(idValues, filterValue, dryRunValue);
            stdOut.Write(FormatBatchResponse(result));
        });
        return cmd;
    }

    private static List<int> ParseIds(string? rawIds)
    {
        if (string.IsNullOrWhiteSpace(rawIds))
        {
            return [];
        }

        var result = new List<int>();
        foreach (var part in rawIds.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out var id))
            {
                result.Add(id);
            }
        }
        return result;
    }

    private static Response FormatBatchResponse(Response<SyncBatchResult> response)
    {
        if (!response.Success || response.Value == null)
        {
            return Response.Fail(response.Message ?? "The operation failed.");
        }

        var batch = response.Value;
        var sb = new StringBuilder();
        foreach (var item in batch.Items)
        {
            sb.AppendLine($"[{item.Outcome}] #{item.RecordId} {item.Title} - {item.Message}");
        }
        sb.AppendLine();
        sb.AppendLine($"Succeeded: {batch.SucceededCount}, Unmapped: {batch.UnmappedCount}, Mismatch: {batch.MismatchCount}, Failed: {batch.FailedCount}, Skipped: {batch.SkippedCount}, Total: {batch.Items.Count}");

        return batch.FailedCount > 0
            ? Response.Fail(sb.ToString())
            : Response.Ok(sb.ToString());
    }
}
