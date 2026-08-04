using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Text;

using Adr.Cli.Services;
using Adr.Cli.Sync;

using McpCore;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class ProjectPlanningSetup
{
    public static Command NewTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-new", "Create a new task: a concrete unit of work to be done, tracked in this project's own planning folder (not an architectural decision - see 'new' for that).");
        cmd.Aliases.Add("new-task");
        cmd.Aliases.Add("nt");

        var title = CommandOptions.Title;
        var description = new Option<string>("--description", "-d") { Description = "Description of the task." };
        var dueDate = new Option<string>("--dueDate") { Description = "Due date for the task." };
        var useAi = new Option<bool>("--ai") { Description = "Draft the Description (if not supplied) and Details for this task using the configured AI provider (see AI-Setup.md). No-op if no provider is configured in adr.config.json." };

        title.Required = true;

        cmd.Options.Add(title);
        cmd.Options.Add(description);
        cmd.Options.Add(dueDate);
        cmd.Options.Add(useAi);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var titleValue = ctx.GetValue(title) ?? "";
            var descriptionValue = ctx.GetValue(description) ?? "";
            var dueDateValue = ctx.GetValue(dueDate);
            var useAiValue = ctx.GetValue(useAi);

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.NewTaskAsync(titleValue, descriptionValue, dueDateValue, useAiValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command ListTasksCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-list", "List all tasks");
        cmd.Aliases.Add("list-tasks");
        cmd.Aliases.Add("tasks");

        var sortReverse = CommandOptions.SortReverse;
        var verbose = CommandOptions.Verbose;

        cmd.Options.Add(sortReverse);
        cmd.Options.Add(verbose);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sortReverseValue = ctx.GetValue(sortReverse);
            var verboseValue = ctx.GetValue(verbose);

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.ListTasksAsync(sortReverseValue, verboseValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command FindTasksCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-find", "Find tasks using a filter");
        cmd.Aliases.Add("find-task");
        cmd.Aliases.Add("ft");

        var filter = CommandOptions.Filter;
        var status = new Option<PlanningStatus>("--status") { Description = "Filter by task status." };
        var sortReverse = CommandOptions.SortReverse;
        var verbose = CommandOptions.Verbose;
        var includeContent = CommandOptions.IncludeContent;

        filter.Required = true;

        cmd.Options.Add(filter);
        cmd.Options.Add(status);
        cmd.Options.Add(sortReverse);
        cmd.Options.Add(verbose);
        cmd.Options.Add(includeContent);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var filterValue = ctx.GetValue(filter) ?? "";
            var statusValue = ctx.GetValue(status);
            var sortReverseValue = ctx.GetValue(sortReverse);
            var verboseValue = ctx.GetValue(verbose);
            var includeContentValue = ctx.GetValue(includeContent);

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.FindTasksAsync(filterValue, statusValue, sortReverseValue, verboseValue, includeContentValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command UpdateTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-update", "Update a task's status");
        cmd.Aliases.Add("update-task");
        cmd.Aliases.Add("sts");

        var sourceId = CommandOptions.SourceId;
        var status = new Option<PlanningStatus>("--status") { Description = "New status for the task." };
        var justification = new Option<string>("--justification", "-j") { Description = "Justification for the status change." };

        sourceId.Required = true;
        sourceId.Aliases.Add("-s");
        status.Required = true;

        cmd.Options.Add(sourceId);
        cmd.Options.Add(status);
        cmd.Options.Add(justification);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var statusValue = ctx.GetValue(status);
            var justificationValue = ctx.GetValue(justification) ?? "";

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.UpdateTaskAsync(sourceIdValue, statusValue, justificationValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command LinkTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-link", "Link two tasks together");
        cmd.Aliases.Add("link-task");
        cmd.Aliases.Add("lt");

        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;
        var remark = new Option<string>("--remark", "-r") { Description = "Remark explaining the relationship." };

        sourceId.Required = true;
        sourceId.Aliases.Add("-s");
        targetId.Required = true;
        targetId.Aliases.Add("-t");

        cmd.Options.Add(sourceId);
        cmd.Options.Add(targetId);
        cmd.Options.Add(remark);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var targetIdValue = ctx.GetValue(targetId) ?? "";
            var remarkValue = ctx.GetValue(remark) ?? "";

            if (!(int.TryParse(sourceIdValue, out var sourceIdInt) && int.TryParse(targetIdValue, out var targetIdInt)))
            {
                stdOut.WriteLine("Source id and target id should be valid identifiers.");
                return;
            }

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.LinkTaskAsync(sourceIdInt, targetIdInt, remarkValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command UnlinkTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-unlink", "Remove link between two tasks");
        cmd.Aliases.Add("unlink-task");
        cmd.Aliases.Add("ult");
        var sourceId = CommandOptions.SourceId;
        var targetId = CommandOptions.TargetId;

        sourceId.Required = true;
        sourceId.Aliases.Add("-s");
        targetId.Required = true;
        targetId.Aliases.Add("-t");

        cmd.Options.Add(sourceId);
        cmd.Options.Add(targetId);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var sourceIdValue = ctx.GetValue(sourceId) ?? "";
            var targetIdValue = ctx.GetValue(targetId) ?? "";

            if (!(int.TryParse(sourceIdValue, out var sourceIdInt) && int.TryParse(targetIdValue, out var targetIdInt)))
            {
                stdOut.WriteLine("Source id and target id should be valid identifiers.");
                return;
            }

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.RemoveTaskLinkAsync(sourceIdInt, targetIdInt);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command GenerateTocCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-toc", "Generate tasks-toc.md, a table of contents for open tasks, in the parent folder of the configured tasks folder.");

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.GeneratePlanningTocAsync();
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command ExportTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-export", "Export tasks to the currently configured sync provider (see ADR 00008), creating or updating each task's external item and pushing its local status.");

        var ids = new Option<string>("--id") { Description = "Comma or space separated task ids to export. Required unless --filter is given." };
        var filter = CommandOptions.Filter;
        var force = new Option<bool>("--force") { Description = "Skip the remote-divergence check and overwrite the external item with local content regardless. Intended for a single task (--id) at a time." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Report what would be created/updated/mismatched, including local status mapping, without writing anything locally or remotely." };

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

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.ExportTasksAsync(idValues, filterValue, forceValue, dryRunValue);
            stdOut.Write(FormatBatchResponse(result));
        });
        return cmd;
    }

    public static Command ImportTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-import", "Import task status from the currently configured sync provider (see ADR 00008). Defaults to every task with a sync link for the active provider when neither --id nor --filter is given.");

        var ids = new Option<string>("--id") { Description = "Comma or space separated task ids to import." };
        var filter = CommandOptions.Filter;
        var dryRun = new Option<bool>("--dry-run") { Description = "Report what would be imported (status mapping, content pull/mismatch, newly discovered tasks) without writing anything locally or remotely." };

        cmd.Options.Add(ids);
        cmd.Options.Add(filter);
        cmd.Options.Add(dryRun);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var idValues = ParseIds(ctx.GetValue(ids));
            var filterValue = ctx.GetValue(filter);
            var dryRunValue = ctx.GetValue(dryRun);

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.ImportTaskStatusAsync(idValues, filterValue, dryRunValue);
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

    private static Response FormatBatchResponse(Response<TaskSyncBatchResult> response)
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