using CommandHandlers;
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
            context.Console.WriteLine("Ïnitialized");
        });
        app.Add(AdrInitCommandHandler.CommandHandler(serviceProvider));


        return await app.InvokeAsync(args);

        //app.Command("list", (command) =>
        //{
        //    command.Description = "";
        //    command.OnExecute(() => {
        //        return 0;
        //    });
        //});

        //app.Command("new", (command) =>
        //{
        //    command.Description = "Initialize a new ADR record with a title";
        //    var title = command.Argument("title", "");
        //    var context = command.Option("--context|-c", "", CommandOptionType.SingleValue);
        //    var decision = command.Option("--decision|-d", "", CommandOptionType.SingleValue);
        //    var supercedes = command.Option("-s|--supercedes", "", CommandOptionType.SingleValue);
        //    command.HelpOption(HelpOption);

        //    command.OnExecute(() =>
        //    {
        //        var previous = supercedes.Value();
        //        if (!string.IsNullOrEmpty(previous))
        //        {
        //            var settings = AdrSettings.Current;
        //            Console.WriteLine($"Revision for {previous}");
        //            if (!int.TryParse(previous, out var recordId))
        //            {
        //                Console.WriteLine($"Supersedes should be numeric: {previous}");
        //                return -1;
        //            }

        //            var directory = new DirectoryInfo(settings.DocFolder);
        //            if (!directory.Exists)
        //            {
        //                Console.WriteLine($"Could not find folder: {settings.DocFolder}");
        //                return -1;
        //            }
        //            var files = directory.GetFiles(recordId.ToString("D5")+ "*.json");
        //            if (files.Length == 0)
        //            {
        //                Console.WriteLine($"Cannot find superseding record: {previous}");
        //                return -1;
        //            }

        //            var entry = new AdrEntry(
        //                TemplateType.Revision,
        //                title.Value,
        //                context.Value(),
        //                decision.Value());
        //            entry.AdrRecord.SuperSedes = AdrRecord.Load(files[0].FullName);

        //            entry.Write()
        //                 .Launch();
        //        }
        //        else
        //        {
        //            var entry = new AdrEntry(
        //                TemplateType.Adr,
        //                title.Value,
        //                context.Value(),
        //                decision.Value());
        //            entry.Write()
        //                 .Launch();
        //        }
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

        serviceCollection.AddSingleton<IFileSystem, FileSystem>();
        serviceCollection.AddSingleton<IAdrSettings, AdrSettings>();
        serviceCollection.AddSingleton<IAdrInitCommandHandler, AdrInitCommandHandler>();
        serviceCollection.AddSingleton<IAdrRecordRepository, AdrRecordRepository>();
    }

    
}

