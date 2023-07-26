using adr.CommandHandlers;
using adr.Extensions;
using adr.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace adr;



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

        return await app.InvokeAsync(args);

        //app.Command("list", (command) =>
        //{
        //    command.Description = "";
        //    command.OnExecute(() => {
        //        return 0;
        //    });
        //});

        

        //app.Command("link", (command) =>
        //{
        //    command.Description = "";
        //    command.OnExecute(() =>
        //    {
        //        return 0;
        //    });
        //});

        //app.Command("generate", (command) =>
        //{
        //    command.Description = "";
        //    command.OnExecute(() =>
        //    {
        //        return 0;
        //    });
        //});

        //app.OnExecute(() =>
        //{
        //    app.ShowHelp();
        //    return 0;
        //});
        //app.Execute(args);
    }



    private static void ConfigureServices(ServiceCollection serviceCollection)
    {
        serviceCollection.AddLogging(configure =>
                 {
                     configure.SetMinimumLevel(LogLevel.Debug);
                     configure.AddDebug();
                     configure.AddConsole();
                 });

        serviceCollection.AddSingleton<IStdOut, StdOutService>();
        serviceCollection.AddSingleton<IFileSystem, FileSystem>();
        serviceCollection.AddSingleton<IAdrSettings, AdrSettings>();
        serviceCollection.AddSingleton<IAdrRecordRepository, AdrRecordRepository>();

        // Todo: inject commandhandlers based on reflection
        serviceCollection.AddSingleton<IAdrInit, AdrInit>();
        serviceCollection.AddSingleton<IAdrNew, AdrNew>();
        serviceCollection.AddSingleton<IAdrQuery, AdrQuery>();

    }


}

