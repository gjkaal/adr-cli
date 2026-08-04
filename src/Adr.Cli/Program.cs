using System;
using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Adr.Cli.Ai;
using Adr.Cli.Ai.AzureFoundry;
using Adr.Cli.CommandHandlers;
using Adr.Cli.Extensions;
using Adr.Cli.Mcp;
using Adr.Cli.Services;
using Adr.Cli.Sync;
using Adr.Cli.Sync.GitHubProjects;

using McpCore.JsonRpc;
using McpCore.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adr.Cli;

internal static class Program
{
    internal static JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static async Task<int> Main(string[] args)
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Check if running in MCP mode (expecting JSON-RPC over stdin)
        // Support both --mcp flag and mcp command for flexibility
        if (args.Length == 1 && (args[0] == "--mcp" || args[0] == "mcp"))
        {
            return await RunMcpServerAsync(serviceProvider);
        }

        // Traditional CLI mode
        var app = new RootCommand("Command line tool for Architecture Decision Records");

        // Add MCP command
        var mcpCommand = new Command("mcp", "Run as MCP (Model Context Protocol) server");
        mcpCommand.Aliases.Add("--mcp");
        mcpCommand.SetAction(async (ParseResult ctx) => await RunMcpServerAsync(serviceProvider));
        app.Add(mcpCommand);

        app.SetAction((context) =>
        {
            Console.WriteLine("Use -help to see the available commands.");
            Console.WriteLine("Use '--mcp' command to run as MCP server for AI tools.");
        });

        // Show which adr.config.json is currently active
        app.Add(AdrContextSetup.GetContextCommand(serviceProvider));

        // Initialize
        app.Add(CommandHandlerSetup.InitCommand(serviceProvider));
        app.Add(CommandHandlerSetup.SyncMetadataCommand(serviceProvider));
        app.Add(CommandHandlerSetup.GenerateTocCommand(serviceProvider));

        // Create AD, ACR and revisions
        app.Add(AdrNewSetup.NewAdrCommand(serviceProvider));
        app.Add(AdrNewSetup.CopyAdrCommand(serviceProvider));

        // Query the ADR, lists, searching etc.
        app.Add(AdrQuerySetup.QueryCommand(serviceProvider));
        app.Add(AdrQuerySetup.ListCommand(serviceProvider));

        // Link and unlink
        app.Add(AdrLinkSetup.LinkCommand(serviceProvider));
        app.Add(AdrLinkSetup.UnLinkCommand(serviceProvider));
        app.Add(AdrLinkSetup.LinkTaskCommand(serviceProvider));
        app.Add(AdrLinkSetup.UnlinkTaskCommand(serviceProvider));

        // Sync ADRs to GitHub as real Issues, with related tasks as sub-issues (ADR 00010)
        app.Add(AdrGitHubSyncSetup.ExportAdrCommand(serviceProvider));
        app.Add(AdrGitHubSyncSetup.ImportAdrCommand(serviceProvider));

        // Tasks tools
        app.Add(ProjectPlanningSetup.FindTasksCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.LinkTaskCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.UnlinkTaskCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.GenerateTocCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.NewTaskCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.ListTasksCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.UpdateTaskCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.ExportTaskCommand(serviceProvider));
        app.Add(ProjectPlanningSetup.ImportTaskCommand(serviceProvider));

        var parseResult = app.Parse(args);
        var executeResult = parseResult.Invoke();
        return executeResult;
    }

    private static void ConfigureServices(ServiceCollection serviceCollection)
    {
        serviceCollection.AddLogging(configure =>
                 {
#if DEBUG
                     configure.SetMinimumLevel(LogLevel.Debug);
                     configure.AddDebug();
                     configure.AddConsole();
#else
                     configure.SetMinimumLevel(LogLevel.Warning);
#endif
                 });

        serviceCollection.AddSingleton<IProcessHelper, ProcessHelper>();
        serviceCollection.AddSingleton<IStdOut, StdOutService>();
        serviceCollection.AddSingleton<IFileSystem, FileSystem>();
        serviceCollection.AddSingleton<IFileLock, FileLockService>();
        serviceCollection.AddSingleton<IAdrSettings, AdrSettings>();
        serviceCollection.AddSingleton<IAdrRecordRepository, AdrRecordRepository>();
        serviceCollection.AddSingleton<IAdrTasksRepository, AdrTasksRepository>();

        serviceCollection.AddSingleton<IAdrInit, AdrInit>();
        serviceCollection.AddSingleton<IAdrNew, AdrNew>();
        serviceCollection.AddSingleton<IAdrQuery, AdrQuery>();
        serviceCollection.AddSingleton<IAdrLink, AdrLink>();
        serviceCollection.AddSingleton<IProjectPlanning, ProjectPlanning>();
        serviceCollection.AddSingleton<IAdrGitHubSync, AdrGitHubSync>();
        serviceCollection.AddSingleton<IAdrContext, AdrContext>();

        serviceCollection.AddSingleton<IAdrProposalGenerator>(sp =>
        {
            var settings = sp.GetRequiredService<IAdrSettings>();
            if (string.IsNullOrWhiteSpace(settings.AiSettings.Provider))
            {
                return new NoOpAdrProposalGenerator();
            }

            var logger = sp.GetRequiredService<ILogger<AzureFoundryProposalGenerator>>();
            return new AzureFoundryProposalGenerator(settings, logger);
        });

        serviceCollection.AddSingleton<ITaskProposalGenerator>(sp =>
        {
            var settings = sp.GetRequiredService<IAdrSettings>();
            if (string.IsNullOrWhiteSpace(settings.AiSettings.Provider))
            {
                return new NoOpTaskProposalGenerator();
            }

            var logger = sp.GetRequiredService<ILogger<AzureFoundryTaskProposalGenerator>>();
            return new AzureFoundryTaskProposalGenerator(settings, logger);
        });

        serviceCollection.AddSingleton<ITaskSyncProvider>(sp =>
        {
            var settings = sp.GetRequiredService<IAdrSettings>();
            return settings.SyncSettings.Provider switch
            {
                "GitHubProjects" => new GitHubProjectsTaskSyncProvider(settings, sp.GetRequiredService<ILogger<GitHubProjectsTaskSyncProvider>>()),
                _ => new NoOpTaskSyncProvider()
            };
        });

        serviceCollection.AddSingleton<IAdrSyncProvider>(sp =>
        {
            var settings = sp.GetRequiredService<IAdrSettings>();
            return settings.SyncSettings.Provider switch
            {
                "GitHubProjects" => new GitHubProjectsAdrSyncProvider(settings, sp.GetRequiredService<ILogger<GitHubProjectsAdrSyncProvider>>()),
                _ => new NoOpAdrSyncProvider()
            };
        });

        // MCP Server
        serviceCollection.AddSingleton<IMcpServer, AdrMcpServer>();
    }

    private static async Task<int> RunMcpServerAsync(IServiceProvider serviceProvider)
    {
        var stdOut = serviceProvider.GetService<IStdOut>();
        if (stdOut != null)
        {
            stdOut.Mute();
        }

        var mcpServer = serviceProvider.GetRequiredService<IMcpServer>();
        var startTime = DateTime.UtcNow;

        Console.Error.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss}] Starting n2adr MCP Server v1.0.0.3");
        Console.Error.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss}] Protocol: JSON-RPC 2.0 over stdio");
        Console.Error.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss}] Working Directory: {Environment.CurrentDirectory}");
        Console.Error.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss}] Ready to accept requests...");

        var requestCount = 0;

        try
        {
            // Read JSON-RPC messages from stdin and write responses to stdout
            while (true)
            {
                var line = await Console.In.ReadLineAsync();
                if (line == null)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] EOF received on stdin. Shutting down gracefully.");
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Total requests processed: {requestCount}");
                    break; // EOF
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                requestCount++;
                var requestStartTime = DateTime.UtcNow;

                try
                {
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
                    if (request == null)
                    {
                        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Warning: Null request after deserialization");
                        continue;
                    }

                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Request #{requestCount}: method='{request.Method}', id={request.Id}");

                    var response = await mcpServer.ProcessRequestAsync(request);

                    // Only send response if this is not a notification (id is not null)
                    // According to JSON-RPC 2.0, notifications don't expect a response
                    if (request.Id != null)
                    {
                        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
                        await Console.Out.WriteLineAsync(responseJson);
                        await Console.Out.FlushAsync();
                    }
                    else
                    {
                        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Notification processed (no response sent)");
                    }

                    var duration = DateTime.UtcNow - requestStartTime;
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Response #{requestCount}: completed in {duration.TotalMilliseconds:F2}ms");
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] JSON parsing error: {ex.Message}");
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Invalid JSON: {line.Substring(0, Math.Min(100, line.Length))}...");

                    // Send JSON-RPC error response
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.ParseError,
                            Message = "Parse error: Invalid JSON"
                        }
                    };

                    var errorJson = JsonSerializer.Serialize(errorResponse, SerializerOptions);
                    await Console.Out.WriteLineAsync(errorJson);
                    await Console.Out.FlushAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Error processing request #{requestCount}: {ex.GetType().Name}: {ex.Message}");
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Stack trace: {ex.StackTrace}");

                    // Try to send error response
                    try
                    {
                        var errorResponse = new JsonRpcResponse
                        {
                            Id = null,
                            Error = new JsonRpcError
                            {
                                Code = JsonRpcErrorCodes.InternalError,
                                Message = $"Internal error: {ex.Message}"
                            }
                        };

                        var errorJson = JsonSerializer.Serialize(errorResponse, SerializerOptions);
                        await Console.Out.WriteLineAsync(errorJson);
                        await Console.Out.FlushAsync();
                    }
                    catch
                    {
                        // Best effort - don't crash if we can't send error response
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Fatal MCP Server error: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Stack trace: {ex.StackTrace}");
            return 1;
        }

        var totalDuration = DateTime.UtcNow - startTime;
        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] MCP Server shut down cleanly after {totalDuration.TotalSeconds:F2}s");
        return 0;
    }
}