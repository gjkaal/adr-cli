using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Adr.Cli.CommandHandlers;
using Adr.Cli.Sync;

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
                Description = "Pin this MCP session to a specific, already-initialized ADR repository, identified by a directory or by project name. Never creates a config file. For a directory: searches upward first (any directory inside the target repository resolves immediately); if nothing is found upward, searches downward into subfolders instead, so pointing this at a multi-repo workspace root discovers the repositories nested under it. For a project name (when the value isn't an existing directory): matched against whatever adr.config.json files the most recent downward search found. If more than one candidate matches (several nested repos, or several projects sharing a name), the response lists each project name and folder path instead of guessing - call this again with one of them. Use this in a multi-repo workspace to avoid silently operating against the wrong project's ADRs.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["workingDirectory"] = new() { Type = "string", Description = "A directory inside, above, or containing the target ADR repository/repositories (e.g. the repo root, a subfolder, or a multi-repo workspace root) - or, if not an existing directory, an ADR project's name (required)." }
                    },
                    Required = new[] { "workingDirectory" }
                }
            },
            new McpTool
            {
                Name = "adr_get_context",
                Description = "Report which adr.config.json (path and ProjectName) is currently active for this MCP session, whether an AI provider is connected (see AI-Setup.md - determines whether adr_new's \"ai\" option is a no-op), and whether a task sync provider is connected (determines whether task_export/task_import are no-ops), without changing anything.",
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
                Description = "Create a new, blank Architecture Decision Record from a template and open it for editing. An ADR records a decision and its consequences - for a unit of work to be done instead, use task_new. To copy an existing ADR's content into a new record instead of starting blank, use adr_copy.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["title"] = new() { Type = "string", Description = "Title for the new ADR." },
                        ["context"] = new() { Type = "string", Description = "Optional user-authored Context for the ADR. If omitted and ai=true, the AI provider drafts one; otherwise the ADR is created with a generic placeholder Context." },
                        ["req"] = new() { Type = "boolean", Description = "Use the Architecture Significant Requirement (ASR) template instead of the standard ADR (decision) template.", Default = false },
                        ["revisionFor"] = new() { Type = "integer", Description = "If set, the existing ADR ID that this new ADR supersedes. Adds a 'Supersedes' link from the new ADR to that one; the old ADR's own content is left unchanged." },
                        ["ai"] = new() { Type = "boolean", Description = "Draft the Context (if not supplied), Decision, and Consequences sections using the configured AI provider (see AI-Setup.md). If omitted, defaults to true when a provider is configured in adr.config.json and false otherwise - pass ai:false to opt out explicitly even when a provider is configured." }
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
                Description = "Regenerate adr-toc.md (a table of every ADR: id, title, status) in the parent folder of the configured ADR docs folder (e.g. docs/adr-toc.md when ADRs live in docs/adr). Overwrites the existing file; run this after adding, linking, or changing the status of ADRs to keep it current.",
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
                Description = "Create a new project planning task: a unit of work to be done, not an architectural decision (use adr_new for that). The tasks folder is created automatically on first use if it doesn't exist yet. New tasks start with status 'New' - use task_update to change status later.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["title"] = new() { Type = "string", Description = "Title for the task." },
                        ["description"] = new() { Type = "string", Description = "Short description of the task. If omitted and ai=true, the AI provider drafts one; otherwise the task is created with a generic placeholder Description." },
                        ["dueDate"] = new() { Type = "string", Description = "Due date, e.g. \"2026-08-01\" (any .NET-parseable date string). Omit if there is no due date." },
                        ["ai"] = new() { Type = "boolean", Description = "Draft the Description (if not supplied) and Details for this task using the configured AI provider (see AI-Setup.md). No-op if no provider is configured in adr.config.json.", Default = false }
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
                Description = "Regenerate tasks-toc.md (the open-tasks table of contents) in the parent folder of the configured tasks folder (e.g. docs/tasks-toc.md when tasks live in docs/planning). Lists only tasks that are not Completed, Abandoned, or None - closed-out tasks are intentionally left off. Overwrites the existing file; run this after adding, linking, or updating the status of tasks to keep it current.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>(),
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "task_export",
                Description = "Export tasks to the currently configured sync provider (see ADR 00008/00009), creating or updating each task's external item and pushing its local title/description/details and status. On an existing link, refuses to overwrite content that changed remotely since the last sync (reported as \"Mismatch\") unless force is set. Requires either taskIds or query - unlike task_import, there is no all-tasks default. One task failing does not stop the rest of the batch; see the returned per-task outcomes (Succeeded/Unmapped/Mismatch/Failed) and counts.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["taskIds"] = new() { Type = "string", Description = "Comma or space separated task ids to export. Takes priority over query when non-empty." },
                        ["query"] = new() { Type = "string", Description = "task_find-style word filter against title/description, used when taskIds is omitted." },
                        ["force"] = new() { Type = "boolean", Description = "Skip the remote-divergence check and overwrite the external item with local content regardless, even if it was edited remotely since the last sync. A deliberate override, not the default - intended for a single task (taskIds with one id) at a time, since forcing a multi-task batch can silently discard several remote edits at once.", Default = false },
                        ["dryRun"] = new() { Type = "boolean", Description = "Report what each task's export would do (create/update/mismatch, status mapping) without writing anything locally or to the external provider.", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "task_import",
                Description = "Import status - and, when safe, title/description/details - from the currently configured sync provider (see ADR 00008/00009) for each selected task. Content is only pulled when local hasn't changed since the last sync; if both sides diverged, the task is reported as \"Mismatch\" and neither side is touched. When both taskIds and query are omitted, defaults to every task with a sync link for the currently active provider (a task whose only link belongs to a different, no-longer-active provider is reported as skipped) plus any item on the external board with no local counterpart yet, which is adopted as a brand-new local task. One task failing does not stop the rest of the batch; see the returned per-task outcomes (Succeeded/Unmapped/Mismatch/Skipped/Failed) and counts.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["taskIds"] = new() { Type = "string", Description = "Comma or space separated task ids to import. Takes priority over query when non-empty." },
                        ["query"] = new() { Type = "string", Description = "task_find-style word filter against title/description, used when taskIds is omitted." },
                        ["dryRun"] = new() { Type = "boolean", Description = "Report what each task's import would do (status mapping, content pull/mismatch, newly discovered tasks) without writing anything locally or to the external provider - including skipping the creation of any newly discovered task.", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_link_task",
                Description = "Record that a task belongs to an ADR (AdrRecord.RelatedTasks, see ADR 00010), so adr_export can attach it as a GitHub sub-issue of the ADR's Issue. Metadata-only - no markdown content is edited.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["adrId"] = new() { Type = "integer", Description = "ADR ID that the task belongs to." },
                        ["taskId"] = new() { Type = "integer", Description = "Task ID being related to the ADR." },
                        ["remark"] = new() { Type = "string", Description = "Short note on the relationship." }
                    },
                    Required = new[] { "adrId", "taskId" }
                }
            },
            new McpTool
            {
                Name = "adr_unlink_task",
                Description = "Remove a task from an ADR's related tasks (AdrRecord.RelatedTasks, see ADR 00010).",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["adrId"] = new() { Type = "integer", Description = "ADR ID to remove the task from." },
                        ["taskId"] = new() { Type = "integer", Description = "Task ID currently related to the ADR, to stop relating." }
                    },
                    Required = new[] { "adrId", "taskId" }
                }
            },
            new McpTool
            {
                Name = "adr_export",
                Description = "Export ADRs to the currently configured sync provider as real GitHub Issues (see ADR 00010), creating or updating each ADR's Issue, pushing its local status, and attaching each related task (AdrRecord.RelatedTasks) as a GitHub sub-issue - promoting a task to a real Issue first if it is currently only a Draft Issue. On an existing link, refuses to overwrite content that changed remotely since the last sync (reported as \"Mismatch\") unless force is set. Requires either adrIds or query - there is no all-ADRs default. One ADR failing does not stop the rest of the batch.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["adrIds"] = new() { Type = "string", Description = "Comma or space separated ADR ids to export. Takes priority over query when non-empty." },
                        ["query"] = new() { Type = "string", Description = "adr_find-style word filter against title/context, used when adrIds is omitted." },
                        ["force"] = new() { Type = "boolean", Description = "Skip the remote-divergence check and overwrite the external issue with local content regardless. A deliberate override, not the default - intended for a single ADR (adrIds with one id) at a time.", Default = false },
                        ["dryRun"] = new() { Type = "boolean", Description = "Report what each ADR's export (and each related task's promotion/attachment) would do without writing anything locally or to the external provider.", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_import",
                Description = "Import status - and, when safe, content - from the currently configured sync provider (see ADR 00010) for each selected ADR. When both adrIds and query are omitted, defaults to every ADR with a sync link for the currently active provider - unlike task_import, there is no discovery/adoption of board-only items as brand-new local ADRs. One ADR failing does not stop the rest of the batch.",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["adrIds"] = new() { Type = "string", Description = "Comma or space separated ADR ids to import. Takes priority over query when non-empty." },
                        ["query"] = new() { Type = "string", Description = "adr_find-style word filter against title/context, used when adrIds is omitted." },
                        ["dryRun"] = new() { Type = "boolean", Description = "Report what each ADR's import would do without writing anything locally or to the external provider.", Default = false }
                    },
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
                "task_export" => await HandleTaskExportAsync(parameters.Arguments),
                "task_import" => await HandleTaskImportAsync(parameters.Arguments),
                "adr_link_task" => await HandleAdrLinkTaskAsync(parameters.Arguments),
                "adr_unlink_task" => await HandleAdrUnlinkTaskAsync(parameters.Arguments),
                "adr_export" => await HandleAdrExportAsync(parameters.Arguments),
                "adr_import" => await HandleAdrImportAsync(parameters.Arguments),
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
        var aiLabel = context.AiConfigured ? $"connected ({context.AiProvider})" : "not connected";
        var syncLabel = context.SyncConfigured ? $"connected ({context.SyncProvider})" : "not connected";
        return $"{text}{Environment.NewLine}{Environment.NewLine}[adr-cli context: project=\"{context.ProjectName}\", config={configLabel}, ai={aiLabel}, sync={syncLabel}]";
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
        var context = GetStringArgument(arguments, "context") ?? string.Empty;
        var req = GetBoolArgument(arguments, "req");
        var revisionFor = GetIntArgument(arguments, "revisionFor") ?? 0;
        var useAi = GetNullableBoolArgument(arguments, "ai");

        var result = await adrNew.NewAdrAsync(title, req, revisionFor.ToString(), context, useAi);
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
        var useAi = GetBoolArgument(arguments, "ai");

        var result = await projectPlanning.NewTaskAsync(title, description, dueDate, useAi);
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

    private async Task<string> HandleTaskExportAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var taskIds = ParseIdList(GetStringArgument(arguments, "taskIds"));
        var query = GetStringArgument(arguments, "query");
        var force = GetBoolArgument(arguments, "force");
        var dryRun = GetBoolArgument(arguments, "dryRun");

        var result = await projectPlanning.ExportTasksAsync(taskIds, query, force, dryRun);
        return result.Success && result.Value != null ? FormatBatchResult(result.Value) : $"Failed: {result.Message}";
    }

    private async Task<string> HandleTaskImportAsync(Dictionary<string, object?> arguments)
    {
        var projectPlanning = _serviceProvider.GetRequiredService<IProjectPlanning>();

        var taskIds = ParseIdList(GetStringArgument(arguments, "taskIds"));
        var query = GetStringArgument(arguments, "query");
        var dryRun = GetBoolArgument(arguments, "dryRun");

        var result = await projectPlanning.ImportTaskStatusAsync(taskIds, query, dryRun);
        return result.Success && result.Value != null ? FormatBatchResult(result.Value) : $"Failed: {result.Message}";
    }


    private async Task<string> HandleAdrLinkTaskAsync(Dictionary<string, object?> arguments)
    {
        var adrLink = _serviceProvider.GetRequiredService<IAdrLink>();

        var adrId = GetIntArgument(arguments, "adrId") ?? throw new ArgumentException("ADR ID is required");
        var taskId = GetIntArgument(arguments, "taskId") ?? throw new ArgumentException("Task ID is required");
        var remark = GetStringArgument(arguments, "remark") ?? string.Empty;

        var result = await adrLink.LinkAdrToTaskAsync(adrId, taskId, remark);
        return result.Success ? result.Message ?? "Task linked to ADR successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrUnlinkTaskAsync(Dictionary<string, object?> arguments)
    {
        var adrLink = _serviceProvider.GetRequiredService<IAdrLink>();

        var adrId = GetIntArgument(arguments, "adrId") ?? throw new ArgumentException("ADR ID is required");
        var taskId = GetIntArgument(arguments, "taskId") ?? throw new ArgumentException("Task ID is required");

        var result = await adrLink.RemoveAdrTaskLinkAsync(adrId, taskId);
        return result.Success ? result.Message ?? "Task unlinked from ADR successfully" : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrExportAsync(Dictionary<string, object?> arguments)
    {
        var adrSync = _serviceProvider.GetRequiredService<IAdrGitHubSync>();

        var adrIds = ParseIdList(GetStringArgument(arguments, "adrIds"));
        var query = GetStringArgument(arguments, "query");
        var force = GetBoolArgument(arguments, "force");
        var dryRun = GetBoolArgument(arguments, "dryRun");

        var result = await adrSync.ExportAdrAsync(adrIds, query, force, dryRun);
        return result.Success && result.Value != null ? FormatBatchResult(result.Value) : $"Failed: {result.Message}";
    }

    private async Task<string> HandleAdrImportAsync(Dictionary<string, object?> arguments)
    {
        var adrSync = _serviceProvider.GetRequiredService<IAdrGitHubSync>();

        var adrIds = ParseIdList(GetStringArgument(arguments, "adrIds"));
        var query = GetStringArgument(arguments, "query");
        var dryRun = GetBoolArgument(arguments, "dryRun");

        var result = await adrSync.ImportAdrStatusAsync(adrIds, query, dryRun);
        return result.Success && result.Value != null ? FormatBatchResult(result.Value) : $"Failed: {result.Message}";
    }

    private static List<int> ParseIdList(string? rawIds)
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

    private static string FormatBatchResult(SyncBatchResult batch)
    {
        var sb = new StringBuilder();
        foreach (var item in batch.Items)
        {
            sb.AppendLine($"[{item.Outcome}] #{item.RecordId} {item.Title} - {item.Message}");
        }
        sb.AppendLine();
        sb.AppendLine($"Succeeded: {batch.SucceededCount}, Unmapped: {batch.UnmappedCount}, Mismatch: {batch.MismatchCount}, Failed: {batch.FailedCount}, Skipped: {batch.SkippedCount}, Total: {batch.Items.Count}");
        return sb.ToString();
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

    /// <summary>
    /// Like <see cref="GetBoolArgument" /> but distinguishes "argument omitted" (null) from an
    /// explicit true/false, so a caller can default an omitted flag based on server-side state
    /// (e.g. whether an AI provider is configured) instead of always defaulting to false.
    /// </summary>
    private static bool? GetNullableBoolArgument(Dictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.GetBoolean();
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        return bool.TryParse(value.ToString(), out var result) ? result : null;
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