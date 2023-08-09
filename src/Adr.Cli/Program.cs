using Adr.Cli.CommandHandlers;
using Adr.Cli.Extensions;
using Adr.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace Adr.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var app = new RootCommand("Command line tool for Architecture Decision Records");

        app.SetHandler((context) =>
        {
            context.Console.WriteLine("Use -help to see the available commands.");
        });

        // Initialize
        app.Add(CommandHandlerSetup.InitCommand(serviceProvider));
        app.Add(CommandHandlerSetup.SyncMetadataCommand(serviceProvider));
        app.Add(CommandHandlerSetup.GenerateTocCommand(serviceProvider));

        // Create AD, ACR and revisions
        app.Add(AdrNewSetup.NewAdrCommand(serviceProvider));

        // Query the ADR, lists, searching etc.
        app.Add(AdrQuerySetup.QueryCommand(serviceProvider));
        app.Add(AdrQuerySetup.ListCommand(serviceProvider));

        // Link and unlink
        app.Add(AdrLinkSetup.LinkCommand(serviceProvider));
        app.Add(AdrLinkSetup.UnLinkCommand(serviceProvider));

        return await app.InvokeAsync(args);
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

        serviceCollection.AddSingleton<IAdrInit, AdrInit>();
        serviceCollection.AddSingleton<IAdrNew, AdrNew>();
        serviceCollection.AddSingleton<IAdrQuery, AdrQuery>();
        serviceCollection.AddSingleton<IAdrLink, AdrLink>();
    }
}