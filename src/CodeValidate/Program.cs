namespace CodeValidate;

internal class Program
{
    private static int Main(string[] args)
    {
        // Use the arguments to check which validator to call
        if (args.Length == 0)
        {
            Console.WriteLine("No validator specified");
            return -1;
        }

        var logToFile = args.Contains("-log");
        var silent = args.Contains("-silent");
        var verbose = args.Contains("-verbose");
        var stdIo = new StdIo(logToFile, silent, verbose);
        var ignoreList = FindIgnoreList(args);

        IValidator validator;
        switch (args[0])
        {
            case "cs-namespace":
                validator = new CSharpNamespaceValidator(args[1..], stdIo, ignoreList);
                break;
            case "-help":
                Console.WriteLine("Usage: CodeValidate <validator> <directory> [-log] [-silent] [-verbose] [-ignore:<file>]");
                Console.WriteLine("Validators:");
                Console.WriteLine("  cs-namespace: Validates the namespace of all files in the specified directory");
                Console.WriteLine("Options:");
                Console.WriteLine("  -log:     Save the output to a logfile");
                Console.WriteLine("  -silent:  Do not write to the console");
                Console.WriteLine("  -verbose: Write verbose output to the console and the logfile");
                Console.WriteLine("  -ignore:  Ignore the specified file (partial name)");
                return 0;
            default:
                Console.WriteLine($"Unknown validator: {args[0]}.");
                return -1;
        }

        var result = validator.Validate();
        stdIo.SaveLogfile();
        return result;
    }

    private static string[] FindIgnoreList(string[] args) { 
        var ignoreList = new List<string>();
        foreach(var arg in args)
        {
            if (arg.StartsWith("-ignore:"))
            {
                ignoreList.Add( arg[8..]);
            }
        }
        return ignoreList.ToArray();
    }
}