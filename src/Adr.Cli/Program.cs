using System;
using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;

using Adr.Cli.CommandHandlers;
using Adr.Cli.Extensions;
using Adr.Cli.Mcp;
using Adr.Cli.Services;

using McpCore.JsonRpc;
using McpCore.Server;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adr.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Check if running in MCP mode (expecting JSON-RPC over stdin)
        if (args.Length == 1 && args[0] == "--mcp")
        {
            return await RunMcpServerAsync(serviceProvider);
        }

        // Traditional CLI mode
        var app = new RootCommand("Command line tool for Architecture Decision Records");

        // Add MCP command
        var mcpCommand = new Command("mcp", "Run as MCP (Model Context Protocol) server");
        mcpCommand.SetAction(async (ParseResult ctx) => await RunMcpServerAsync(serviceProvider));
        app.Add(mcpCommand);

        app.SetAction((context) =>
        {
            Console.WriteLine("Use -help to see the available commands.");
            Console.WriteLine("Use 'mcp' command to run as MCP server for AI tools.");
        });

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
        serviceCollection.AddSingleton<IAdrSettings, AdrSettings>();
        serviceCollection.AddSingleton<IAdrRecordRepository, AdrRecordRepository>();
        serviceCollection.AddSingleton<IAdrTasksRepository, AdrTasksRepository>();

        serviceCollection.AddSingleton<IAdrInit, AdrInit>();
        serviceCollection.AddSingleton<IAdrNew, AdrNew>();
        serviceCollection.AddSingleton<IAdrQuery, AdrQuery>();
        serviceCollection.AddSingleton<IAdrLink, AdrLink>();
        serviceCollection.AddSingleton<IProjectPlanning, ProjectPlanning>();

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

        Console.Error.WriteLine("Starting ADR CLI MCP Server...");

        try
        {
            // Read JSON-RPC messages from stdin and write responses to stdout
            while (true)
            {
                var line = await Console.In.ReadLineAsync();
                if (line == null)
                {
                    break; // EOF
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line);
                    if (request == null)
                    {
                        continue;
                    }

                    var response = await mcpServer.ProcessRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response);

                    await Console.Out.WriteLineAsync(responseJson);
                    await Console.Out.FlushAsync();
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"JSON parsing error: {ex.Message}");

                    // Send JSON-RPC error response
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError
                        {
                            Code = JsonRpcErrorCodes.ParseError,
                            Message = "Parse error"
                        }
                    };

                    var errorJson = JsonSerializer.Serialize(errorResponse);
                    await Console.Out.WriteLineAsync(errorJson);
                    await Console.Out.FlushAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing request: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MCP Server error: {ex.Message}");
            return 1;
        }

        return 0;
    }
}