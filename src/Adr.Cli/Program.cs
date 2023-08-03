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

        //var handler = serviceProvider.GetRequiredService<InitializeAdrHandler>();

        var app = new RootCommand("Command line tool for Architecture Decision Records");

        app.SetHandler((context) =>
        {
            context.Console.WriteLine("Use -help to see the available commands.");
        });

        // Initialize
        app.AddRange(AdrInit.CommandHandler(serviceProvider));

        // Create AD, ACR and revisions
        app.AddRange(AdrNew.CommandHandler(serviceProvider));

        // Query the ADR, lists, searching etc.
        app.AddRange(AdrQuery.CommandHandler(serviceProvider));

        // Add links etc.
        app.AddRange(AdrLink.CommandHandler(serviceProvider));

        return await app.InvokeAsync(args);

        //app.Command("generate", (command) =>
        //{
        //    command.Description = "generate a PDF containing the complete ADR set";
        //    command.OnExecute(() =>
        //    {
        //        return 0;
        //    });
        //});
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

        // Todo: inject commandhandlers based on reflection
        serviceCollection.AddSingleton<IAdrInit, AdrInit>();
        serviceCollection.AddSingleton<IAdrNew, AdrNew>();
        serviceCollection.AddSingleton<IAdrQuery, AdrQuery>();
        serviceCollection.AddSingleton<IAdrLink, AdrLink>();

    }
}

