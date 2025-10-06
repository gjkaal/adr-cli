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
            new McpServerInfo { Name = "adr-cli", Version = "1.0.0.2" },
            new McpServerCapabilities { Tools = new McpToolsCapability() }
        )
    {
        _serviceProvider = serviceProvider;
    }

    protected override McpTool[] GetAvailableTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "adr_init",
                Description = "Initialize a new ADR and planning repository in the current directory",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["adrRoot"] = new() { Type = "string", Description = "Custom ADR root directory path (optional)" },
                        ["tmpRoot"] = new() { Type = "string", Description = "Custom template root directory path (optional)" },
                        ["prjRoot"] = new() { Type = "string", Description = "Custom project planning root directory path (optional)" }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_new",
                Description = "Create a new Architecture Decision Record",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["title"] = new() { Type = "string", Description = "Title for the new ADR (required)" },
                        ["req"] = new() { Type = "boolean", Description = "Mark as architectural requirement", Default = false },
                        ["rev"] = new() { Type = "integer", Description = "ADR ID to revise (creates a revision of existing ADR)" }
                    },
                    Required = new[] { "title" }
                }
            },
            new McpTool
            {
                Name = "adr_list",
                Description = "List all Architecture Decision Records",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["desc"] = new() { Type = "boolean", Description = "Show ADRs in descending order (latest first)", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Show detailed information", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_find",
                Description = "Search for Architecture Decision Records",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["query"] = new() { Type = "string", Description = "Search query text (required)" },
                        ["full"] = new() { Type = "boolean", Description = "Search full content (slower)", Default = false },
                        ["desc"] = new() { Type = "boolean", Description = "Show results in descending order", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Show detailed information", Default = false }
                    },
                    Required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "adr_link",
                Description = "Link two ADRs together with a relationship",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Source ADR ID (required)" },
                        ["target"] = new() { Type = "integer", Description = "Target ADR ID (required)" },
                        ["reason"] = new() { Type = "string", Description = "Reason for the link (optional)" }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "adr_unlink",
                Description = "Remove links between two ADRs",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Source ADR ID (required)" },
                        ["target"] = new() { Type = "integer", Description = "Target ADR ID (required)" }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "adr_copy",
                Description = "Copy an existing ADR to create a new one",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Source ADR ID to copy (required)" },
                        ["rev"] = new() { Type = "boolean", Description = "Create as revision", Default = false }
                    },
                    Required = new[] { "source" }
                }
            },
            new McpTool
            {
                Name = "adr_sync",
                Description = "Synchronize metadata with markdown content",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["record"] = new() { Type = "integer", Description = "Specific record ID to sync (optional)" }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "adr_generate_toc",
                Description = "Generate table of contents for ADR repository",
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
                Description = "Create a new task for project planning",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["title"] = new() { Type = "string", Description = "Title for the task (required)" },
                        ["description"] = new() { Type = "string", Description = "Description of the task" },
                        ["dueDate"] = new() { Type = "string", Description = "Due date for the task (ISO format or parseable date string)" }
                    },
                    Required = new[] { "title" }
                }
            },
            new McpTool
            {
                Name = "task_list",
                Description = "List all tasks",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["desc"] = new() { Type = "boolean", Description = "Show tasks in descending order (latest first)", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Show detailed information", Default = false }
                    },
                    Required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "task_find",
                Description = "Find tasks using a filter",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["query"] = new() { Type = "string", Description = "Search query text (required)" },
                        ["status"] = new() { Type = "string", Description = "Filter by status (None, New, OnHold, Planned, Active, Related, ReviewPending, ReviewComplete, AcceptancePending, Completed, Abandoned)" },
                        ["includeContent"] = new() { Type = "boolean", Description = "Search full content (slower)", Default = false },
                        ["desc"] = new() { Type = "boolean", Description = "Show results in descending order", Default = false },
                        ["verbose"] = new() { Type = "boolean", Description = "Show detailed information", Default = false }
                    },
                    Required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "task_update",
                Description = "Update a task's status",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["taskId"] = new() { Type = "integer", Description = "Task ID to update (required)" },
                        ["status"] = new() { Type = "string", Description = "New status (New, OnHold, Planned, Active, Related, ReviewPending, ReviewComplete, AcceptancePending, Completed, Abandoned) (required)" },
                        ["justification"] = new() { Type = "string", Description = "Justification for the status change" }
                    },
                    Required = new[] { "taskId", "status" }
                }
            },
            new McpTool
            {
                Name = "task_link",
                Description = "Link two tasks together",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Source task ID (required)" },
                        ["target"] = new() { Type = "integer", Description = "Target task ID (required)" },
                        ["remark"] = new() { Type = "string", Description = "Remark explaining the relationship" }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "task_unlink",
                Description = "Remove link between two tasks",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>
                    {
                        ["source"] = new() { Type = "integer", Description = "Source task ID (required)" },
                        ["target"] = new() { Type = "integer", Description = "Target task ID (required)" }
                    },
                    Required = new[] { "source", "target" }
                }
            },
            new McpTool
            {
                Name = "task_generate_toc",
                Description = "Generate table of contents for tasks",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpPropertyDefinition>(),
                    Required = Array.Empty<string>()
                }
            }
        };
    }

    public override async Task<McpToolCallResult> CallToolAsync(McpToolCallParams parameters)
    {
        try
        {
            var result = parameters.Name switch
            {
                "adr_init" => await HandleAdrInitAsync(parameters.Arguments),
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
                Content = new[] { new McpContent { Type = "text", Text = result } },
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return new McpToolCallResult
            {
                Content = new[] { new McpContent { Type = "text", Text = $"Error: {ex.Message}" } },
                IsError = true
            };
        }
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

    private async Task<string> HandleAdrNewAsync(Dictionary<string, object?> arguments)
    {
        var adrNew = _serviceProvider.GetRequiredService<IAdrNew>();

        var title = GetStringArgument(arguments, "title") ?? throw new ArgumentException("Title is required");
        var req = GetBoolArgument(arguments, "req");
        var rev = GetIntArgument(arguments, "rev") ?? 0;

        var result = await adrNew.NewAdrAsync(title, req, rev.ToString(), string.Empty);
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

        var record = GetIntArgument(arguments, "record") ?? 1;

        var result = await adrInit.SyncMetadataAsync(record, 0);
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