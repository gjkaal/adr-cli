using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Adr.Cli.CommandHandlers;

using McpCore.Protocol;
using McpCore.Server;

using Microsoft.Extensions.DependencyInjection;

namespace Adr.Cli.Mcp;

/// <summary>
/// MCP server implementation for ADR CLI operations
/// </summary>
public class AdrMcpServer : McpServer
{
    private readonly IServiceProvider _serviceProvider;

    public AdrMcpServer(IServiceProvider serviceProvider)
        : base(
            new McpServerInfo { Name = "n2adr", Version = "1.0.0.3" },
            new McpServerCapabilities { Tools = new McpToolsCapability() }
        )
    {
        _serviceProvider = serviceProvider;
    }

    protected override McpTool[] GetAvailableTools()
    {
        return
        [
            new McpTool
            {
                Name = "adr_init",
                Description = "One-time setup: create adr.config.json and the initial ADR ('Record Architecture Decisions') in the current directory. Safe to call again on an already-initialized repository - it detects existing ADR files and does nothing rather than overwriting them. Call adr_get_context first if you are unsure whether this directory (or an ancestor of it) is already initialized.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["adrRoot"] = new() { Type = "string", Description = "Subfolder name (relative to the current directory) to store ADR files in. Optional, defaults to 'docs\\adr'. Not an absolute path - it always nests under the current directory." },
                        ["tmpRoot"] = new() { Type = "string", Description = "Subfolder name (relative to the current directory) to store markdown templates in. Optional, defaults to 'docs\\adr-templates'." },
                        ["prjRoot"] = new() { Type = "string", Description = "Subfolder name (relative to the current directory) to store task/planning files in. Optional, defaults to 'docs\\planning'." }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_set_context",
                Description = "Pin this MCP session to a specific, already-initialized ADR repository, identified by any directory inside it. Searches upward for an existing adr.config.json; never creates one. Use this in a multi-repo workspace to avoid silently operating against the wrong project's ADRs.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["workingDirectory"] = new() { Type = "string", Description = "Any directory inside the target ADR repository (e.g. the repo root or a subfolder). The nearest adr.config.json found by searching upward from here becomes active for the rest of this session (required)." }
                    },
                    Required = new[] { "workingDirectory" }
                }
            },
            new McpTool
            {
                Name = "adr_get_context",
                Description = "Report which adr.config.json (path and ProjectName) is currently active for this MCP session, without changing anything.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>(),
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_new",
                Description = "Create a new, blank Architecture Decision Record from a template and open it for editing. To copy an existing ADR's content into a new record instead of starting blank, use adr_copy.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["title"] = new() { Type = "string", Description = "Title for the new ADR." },
                        ["req"] = new() { Type = "boolean", Description = "Use the Architecture Significant Requirement (ASR) template instead of the standard ADR (decision) template.", Default = false },
                        ["revisionFor"] = new() { Type = "integer", Description = "If set, the existing ADR ID that this new ADR supersedes. Adds a 'Supersedes' link from the new ADR to that one; the old ADR's own content is left unchanged." }
                    },
                    Required = new[] { "title" }
                }
            },
            new McpTool
            {
                Name = "adr_list",
                Description = "List every ADR in the repository, one line each (id, date, status, title). Use adr_find instead if you want to filter by keyword.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["desc"] = new() { Type = "boolean", Description = "List newest ADR first instead of oldest first.", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Include each ADR's Context on an additional line, instead of just id/date/status/title.", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_find",
                Description = "Search ADRs by keyword. Matches if the query contains any single word found in an ADR's Title or Context (case-insensitive substring match, OR across words) - not an exact-phrase or all-words-required match.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["query"] = new() { Type = "string", Description = "One or more space-separated words. An ADR is returned if any word matches." },
                        ["full"] = new() { Type = "boolean", Description = "Also search the full markdown body (Decision, Consequences, etc.), not just Title/Context. Slower - only the .json metadata is searched by default.", Default = false },
                        ["desc"] = new() { Type = "boolean", Description = "List newest matching ADR first instead of oldest first.", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Include each ADR's Context on an additional line, instead of just id/date/status/title.", Default = false }
                    },
                    Required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "adr_link",
                Description = "Record a relationship from one ADR to another: adds a reference in the source ADR's metadata and a line under its Status section in the markdown (e.g. \"Extends [00002...]\"). One-directional - link the reverse pair separately if you want it to show on both ADRs.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "ADR ID that the relationship is recorded on (the one whose markdown gets the new line)." },
                        ["target"] = new() { Type = "integer", Description = "ADR ID being referenced." },
                        ["reason"] = new() { Type = "string", Description = "Verb phrase describing the relationship, e.g. \"Extends\", \"Supersedes\", \"Amends\", \"Replaced-by\", \"Related to\". Defaults to \"Extends\" if omitted." }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "adr_unlink",
                Description = "Remove all reference links from the source ADR to the target ADR (regardless of what reason/verb they were linked with). Does not remove the reverse link if the target also links back to the source - unlink that direction separately.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "ADR ID to remove the link from." },
                        ["target"] = new() { Type = "integer", Description = "ADR ID currently being referenced, to stop referencing." }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "adr_copy",
                Description = "Duplicate an existing ADR's content into a new ADR record and open it for editing. Use this to start from an existing decision's text rather than a blank template (adr_new).",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "ADR ID to copy content from." },
                        ["rev"] = new() { Type = "boolean", Description = "If true, link the new ADR back to the source with \"Supersedes\" (use when the copy is meant to replace/revise the source). If false, link with \"Copied from\" (no supersession implied).", Default = false }
                    },
                    Required = new[] { "source" }
                }
            },
            new McpTool
            {
                Name = "adr_sync",
                Description = "Re-derive an ADR's .json metadata (Title, Status, etc.) from its .md file's heading and Status section. Use after manually editing an ADR's markdown outside of adr-cli, or to repair metadata that has drifted out of sync with the markdown.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["record"] = new() { Type = "integer", Description = "Sync only this single ADR ID. If omitted (or 0), syncs a range instead, controlled by startAt." },
                        ["startAt"] = new() { Type = "integer", Description = "Only used when 'record' is omitted: sync every ADR with an ID >= this value. Defaults to 1 (sync all ADRs).", Default = 1 }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_generate_toc",
                Description = "Regenerate adr-toc.md (a table of every ADR: id, title, status) in the project root, next to adr.config.json. Overwrites the existing file; run this after adding, linking, or changing the status of ADRs to keep it current.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>(),
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "task_new",
                Description = "Create a new project planning task; the tasks folder is created automatically on first use if it doesn't exist yet. New tasks start with status 'New' - use task_update to change status later.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["title"] = new() { Type = "string", Description = "Title for the task." },
                        ["description"] = new() { Type = "string", Description = "Short description of the task." },
                        ["dueDate"] = new() { Type = "string", Description = "Due date, e.g. \"2026-08-01\" (any .NET-parseable date string). Omit if there is no due date." }
                    },
                    Required = new[] { "title" }
                }
            },
            new McpTool
            {
                Name = "task_list",
                Description = "List every task in the repository, one line each (id, date, status, title). Use task_find instead if you want to filter by keyword or status.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["desc"] = new() { Type = "boolean", Description = "List newest task first instead of oldest first.", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Include each task's Description on an additional line, instead of just id/date/status/title.", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "task_find",
                Description = "Search tasks by keyword, optionally narrowed to a single status. Matches if the query contains any single word found in a task's Title or Description (case-insensitive substring match, OR across words) - not an exact-phrase or all-words-required match.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["query"] = new() { Type = "string", Description = "One or more space-separated words. A task is returned if any word matches." },
                        ["status"] = new()
                        {
                            Type = "string",
                            Description = "Only return tasks with this exact status. Omit (or use \"None\") to return tasks in any status.",
                            Enum = new[] { "None", "New", "OnHold", "Planned", "Active", "Related", "ReviewPending", "ReviewComplete", "AcceptancePending", "Completed", "Abandoned" },
                            Default = "None"
                        },
                        ["includeContent"] = new() { Type = "boolean", Description = "Also search the full markdown body, not just Title/Description. Slower - only the .json metadata is searched by default.", Default = false },
                        ["desc"] = new() { Type = "boolean", Description = "List newest matching task first instead of oldest first.", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Include each task's Description on an additional line, instead of just id/date/status/title.", Default = false }
                    },
                    Required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "task_update",
                Description = "Change a task's status and append a justification entry to its status log (the log is kept, not overwritten - every status change is retained for history).",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["taskId"] = new() { Type = "integer", Description = "Task ID to update." },
                        ["status"] = new()
                        {
                            Type = "string",
                            Description = "The task's new status.",
                            Enum = new[] { "New", "OnHold", "Planned", "Active", "Related", "ReviewPending", "ReviewComplete", "AcceptancePending", "Completed", "Abandoned" }
                        },
                        ["justification"] = new() { Type = "string", Description = "Why the status is changing. Recorded in the task's status log alongside the new status and timestamp." }
                    },
                    Required = new[] { "taskId", "status" }
                }
            },
            new McpTool
            {
                Name = "task_link",
                Description = "Record a relationship from one task to another (e.g. a dependency or duplicate). One-directional - link the reverse pair separately if you want it to show on both tasks.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Task ID that the relationship is recorded on." },
                        ["target"] = new() { Type = "integer", Description = "Task ID being referenced." },
                        ["remark"] = new() { Type = "string", Description = "Short note on why the tasks are related, e.g. \"blocks\", \"duplicates\", \"depends on\"." }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "task_unlink",
                Description = "Remove the relationship from the source task to the target task. Does not remove the reverse link if the target also links back to the source - unlink that direction separately.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Task ID to remove the link from." },
                        ["target"] = new() { Type = "integer", Description = "Task ID currently being referenced, to stop referencing." }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "task_generate_toc",
                Description = "Regenerate the open-tasks table of contents markdown file in the project root. Lists only tasks that are not Completed, Abandoned, or None - closed-out tasks are intentionally left off. Overwrites the existing file; run this after adding, linking, or updating the status of tasks to keep it current.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>(),
                    Required = Array.Empty<string>()
                }
            }
        ];
    }

    public override async Task<McpToolCallResult> CallToolAsync(McpToolCallParams parameters)
    {
        try
        {
            var result = parameters.Name switch
            {
                "adr_init" => await HandleAdrInitAsync(parameters.Arguments),
                "adr_set_context" => await HandleAdrSetContextAsync(parameters.Arguments),
                "adr_get_context" => await HandleAdrGetContextAsync(),
                "adr_new" => await HandleAdrNewAsync(parameters.Arguments),
                "adr_list" => await HandleAdrListAsync(parameters.Arguments),
                "adr_find" => await HandleAdrFindAsync(parameters.Arguments),
                "adr_link" => await HandleAdrLinkAsync(parameters.Arguments),
                "adr_unlink" => await HandleAdrUnlinkAsync(parameters.Arguments),
                "adr_copy" => await HandleAdrCopyAsync(parameters.Arguments),
                "adr_sync" => await HandleAdrSyncAsync(parameters.Arguments),
                "adr_generate_toc" => await HandleAdrGenerateTocAsync(),
                "task_new" => await HandleTaskNewAsync(parameters.Arguments),
                "task_list" => await HandleTaskListAsync(parameters.Arguments),
                "task_find" => await HandleTaskFindAsync(parameters.Arguments),
                "task_update" => await HandleTaskUpdateAsync(parameters.Arguments),
                "task_link" => await HandleTaskLinkAsync(parameters.Arguments),
                "task_unlink" => await HandleTaskUnlinkAsync(parameters.Arguments),
                "task_generate_toc" => await HandleTaskGenerateTocAsync(),
                _ => throw new ArgumentException($"Unknown tool: {parameters.Name}")
            };

            return new McpToolCallResult
            {
                Content = new[] { new McpContent { Type = "text", Text = AppendContextSuffix(result) } },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return new McpToolCallResult
            {
                Content = new[] { new McpContent { Type = "text", Text = AppendContextSuffix($"Error: {ex.Message}") } },
                IsError = true
            };
        }
    }

    /// <summary>
    /// Stamps every tool response with the adr.config.json actually in effect, so a caller can
    /// detect a wrong or stale root instead of it failing silently (see ADR 00004).
    /// </summary>
    private string AppendContextSuffix(string text)
    {
        var context = _serviceProvider.GetRequiredService<IAdrSettings>().CurrentContext;
        var configLabel = context.ConfigFilePath ?? "(none found - using built-in defaults)";
        return $"{text}{Environment.NewLine}{Environment.NewLine}[adr-cli context: project=\"{context.ProjectName}\", config={configLabel}]";
    }

    private async Task<string> HandleAdrInitAsync(Dictionary<string, object?> arguments)
    {
        var adrInit = _serviceProvider.GetRequiredService<IAdrInit>();

        var adrRoot = GetStringArgument(arguments, "adrRoot") ?? "";
        var tmpRoot = GetStringArgument(arguments, "tmpRoot") ?? "";
        var prjRoot = GetStringArgument(arguments, "prjRoot") ?? "";

        var result = await adrInit.InitializeAsync(adrRoot, tmpRoot, prjRoot);
        return result.Success ? result.Message ?? "ADR repository initialized successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrSetContextAsync(Dictionary<string, object?> arguments)
    {
        var adrContext = _serviceProvider.GetRequiredService<IAdrContext>();

        var workingDirectory = GetStringArgument(arguments, "workingDirectory")
            ?? throw new ArgumentException("workingDirectory is required");

        var result = await adrContext.SetContextAsync(workingDirectory);
        return result.Success ? result.Message ?? "Context set" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrGetContextAsync()
    {
        var adrContext = _serviceProvider.GetRequiredService<IAdrContext>();

        var result = await adrContext.GetContextAsync();
        return result.Message ?? "Current context";
    }

    private async Task<string> HandleAdrNewAsync(Dictionary<string, object?> arguments)
    {
        var adrNew = _serviceProvider.GetRequiredService<IAdrNew>();

        var title = GetStringArgument(arguments, "title") ?? throw new ArgumentException("Title is required");
        var req = GetBoolArgument(arguments, "req");
        var revisionFor = GetIntArgument(arguments, "revisionFor") ?? 0;

        var result = await adrNew.NewAdrAsync(title, req, revisionFor.ToString(), string.Empty);
        return result.Success ? result.Message ?? "ADR created successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrListAsync(Dictionary<string, object?> arguments)
    {
        var adrQuery = _serviceProvider.GetRequiredService<IAdrQuery>();

        var desc = GetBoolArgument(arguments, "desc");
        var verbose = GetBoolArgument(arguments, "verbose");

        var result = await adrQuery.ListAdrAsync(desc, verbose);
        return result.Success ? result.Message ?? "Listed ADRs" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrFindAsync(Dictionary<string, object?> arguments)
    {
        var adrQuery = _serviceProvider.GetRequiredService<IAdrQuery>();

        var query = GetStringArgument(arguments, "query") ?? throw new ArgumentException("Query is required");
        var full = GetBoolArgument(arguments, "full");
        var desc = GetBoolArgument(arguments, "desc");
        var verbose = GetBoolArgument(arguments, "verbose");

        var result = await adrQuery.FindAdrAsync(query, full, desc, verbose);
        return result.Success ? result.Message ?? "Search completed" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrLinkAsync(Dictionary<string, object?> arguments)
    {
        var adrLink = _serviceProvider.GetRequiredService<IAdrLink>();

        var source = GetIntArgument(arguments, "source") ?? throw new ArgumentException("Source ADR ID is required");
        var target = GetIntArgument(arguments, "target") ?? throw new ArgumentException("Target ADR ID is required");
        var reason = GetStringArgument(arguments, "reason") ?? string.Empty;

        var result = await adrLink.LinkAdrAsync(source, target, reason);
        return result.Success ? result.Message ?? "ADRs linked successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrUnlinkAsync(Dictionary<string, object?> arguments)
    {
        var adrLink = _serviceProvider.GetRequiredService<IAdrLink>();

        var source = GetIntArgument(arguments, "source") ?? throw new ArgumentException("Source ADR ID is required");
        var target = GetIntArgument(arguments, "target") ?? throw new ArgumentException("Target ADR ID is required");

        var result = await adrLink.RemoveLinkAsync(source, target);
        return result.Success ? result.Message ?? "ADRs unlinked successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrCopyAsync(Dictionary<string, object?> arguments)
    {
        var adrNew = _serviceProvider.GetRequiredService<IAdrNew>();

        var source = GetIntArgument(arguments, "source") ?? throw new ArgumentException("Source ADR ID is required");
        var rev = GetBoolArgument(arguments, "rev");

        var result = await adrNew.CopyAdrAsync(source.ToString(), rev);
        return result.Success ? result.Message ?? "ADR copied successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrSyncAsync(Dictionary<string, object?> arguments)
    {
        var adrInit = _serviceProvider.GetRequiredService<IAdrInit>();

        var record = GetIntArgument(arguments, "record") ?? 0;
        var startAt = GetIntArgument(arguments, "startAt") ?? 1;

        var result = await adrInit.SyncMetadataAsync(startAt, record);
        return result.Success ? result.Message ?? "Metadata synchronized successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrGenerateTocAsync()
    {
        var adrInit = _serviceProvider.GetRequiredService<IAdrInit>();

        var result = await adrInit.GenerateTocAsync();
        return result.Success ? result.Message ?? "Table of contents generated successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskNewAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var title = GetStringArgument(arguments, "title") ?? throw new ArgumentException("Title is required");
        var description = GetStringArgument(arguments, "description") ?? string.Empty;
        var dueDate = GetStringArgument(arguments, "dueDate");

        var result = await projectPlanning.NewTaskAsync(title, description, dueDate);
        return result.Success ? result.Message ?? "Task created successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskListAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var desc = GetBoolArgument(arguments, "desc");
        var verbose = GetBoolArgument(arguments, "verbose");

        var result = await projectPlanning.ListTasksAsync(desc, verbose);
        return result.Success ? result.Message ?? "Listed tasks" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskFindAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var query = GetStringArgument(arguments, "query") ?? throw new ArgumentException("Query is required");
        var statusStr = GetStringArgument(arguments, "status") ?? "None";
        var status = Enum.TryParse<PlanningStatus>(statusStr, true, out var parsedStatus) ? parsedStatus : PlanningStatus.None;
        var includeContent = GetBoolArgument(arguments, "includeContent");
        var desc = GetBoolArgument(arguments, "desc");
        var verbose = GetBoolArgument(arguments, "verbose");

        var result = await projectPlanning.FindTasksAsync(query, status, desc, verbose, includeContent);
        return result.Success ? result.Message ?? "Search completed" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskUpdateAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var taskId = GetIntArgument(arguments, "taskId") ?? throw new ArgumentException("Task ID is required");
        var statusStr = GetStringArgument(arguments, "status") ?? throw new ArgumentException("Status is required");
        var status = Enum.TryParse<PlanningStatus>(statusStr, true, out var parsedStatus)
            ? parsedStatus
            : throw new ArgumentException($"Invalid status: {statusStr}");
        var justification = GetStringArgument(arguments, "justification") ?? string.Empty;

        var result = await projectPlanning.UpdateTaskAsync(taskId.ToString(), status, justification);
        return result.Success ? result.Message ?? "Task updated successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskLinkAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var source = GetIntArgument(arguments, "source") ?? throw new ArgumentException("Source task ID is required");
        var target = GetIntArgument(arguments, "target") ?? throw new ArgumentException("Target task ID is required");
        var remark = GetStringArgument(arguments, "remark") ?? string.Empty;

        var result = await projectPlanning.LinkTaskAsync(source, target, remark);
        return result.Success ? result.Message ?? "Tasks linked successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskUnlinkAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var source = GetIntArgument(arguments, "source") ?? throw new ArgumentException("Source task ID is required");
        var target = GetIntArgument(arguments, "target") ?? throw new ArgumentException("Target task ID is required");

        var result = await projectPlanning.RemoveTaskLinkAsync(source, target);
        return result.Success ? result.Message ?? "Tasks unlinked successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskGenerateTocAsync()
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var result = await projectPlanning.GeneratePlanningTocAsync();
        return result.Success ? result.Message ?? "Task table of contents generated successfully" : $"Failed: {result.Message}";
    }

    private static string? GetStringArgument(Dictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.GetString();
        }

        return value.ToString();
    }

    private static bool GetBoolArgument(Dictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value == null)
        {
            return false;
        }

        if (value is JsonElement element)
        {
            return element.GetBoolean();
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        return bool.TryParse(value.ToString(), out var result) && result;
    }

    private static int? GetIntArgument(Dictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.GetInt32();
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return int.TryParse(value.ToString(), out var result) ? result : null;
    }
}