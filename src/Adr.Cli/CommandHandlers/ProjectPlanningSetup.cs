using System;
using System.CommandLine;

using Adr.Cli.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.CommandHandlers;

public static class ProjectPlanningSetup
{
    public static Command NewTaskCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-new", "Create a new task for project planning");
        var title = CommandOptions.Title;
        var description = new Option<string>("--description", "-d") { Description = "Description of the task." };
        var dueDate = new Option<string>("--dueDate") { Description = "Due date for the task." };

        title.Required = true;

        cmd.Options.Add(title);
        cmd.Options.Add(description);
        cmd.Options.Add(dueDate);

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var titleValue = ctx.GetValue(title) ?? "";
            var descriptionValue = ctx.GetValue(description) ?? "";
            var dueDateValue = ctx.GetValue(dueDate);

            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.NewTaskAsync(titleValue, descriptionValue, dueDateValue);
            stdOut.Write(result);
        });
        return cmd;
    }

    public static Command ListTasksCommand(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetRequiredService<IStdOut>();
        var cmd = new Command("task-list", "List all tasks");
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
        var cmd = new Command("task-toc", "Generate table of contents for tasks");

        cmd.SetAction(async (ParseResult ctx) =>
        {
            var c = serviceProvider.GetRequiredService<IProjectPlanning>();
            var result = await c.GeneratePlanningTocAsync();
            stdOut.Write(result);
        });
        return cmd;
    }
}