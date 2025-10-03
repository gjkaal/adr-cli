using System.CommandLine;

namespace Adr.Cli.CommandHandlers;

public static class CommandOptions
{
    public static Option<int> delayOption = new("--delay", "-d")
    {
        Description = "An option whose argument is parsed as an int.",
        DefaultValueFactory = parseResult => 42,
    };

    public static Option<string> AdrRoot => new("--adrRoot") { Description = "Set the adr root directory.", DefaultValueFactory = (a) => string.Empty };

    public static Option<string> Context => new("--context") { Description = "Optional context for the ADR (otherwise a default value will be used)." };

    public static Option<string> Filter => new("-q") { Description = "Only show an ADR if a word or words in used the ADR." };
    public static Option<bool> IncludeContent => new("--full") { Description = "Search the full records (slow)." };
    public static Option<string> Reason => new("--reason") { Description = "The reason for the link." };
    public static Option<string> Record => new("--record") { Description = "Synchronize only for a single record." };
    public static Option<bool> Requirement => new("--req", "-q") { Description = "The ADR is a critical requirement." };
    public static Option<string> Revision => new("--revisionFor", "-u") { Description = "The ADR revision for an earlier ADR, provide a valid id." };
    public static Option<bool> AsRevision => new("--rev") { Description = "Create the new record as a revision." };
    public static Option<bool> SortReverse => new("--desc") { Description = "Show the ADR's with the latest ADR first." };
    public static Option<string> SourceId => new("--source", "-s") { Description = "The source ADR identification (numeric value)." };
    public static Option<string> StartAt => new("--startAt") { Description = "Synchronize from this record until the end." };
    public static Option<string> TargetId => new("--target", "-t") { Description = "The target ADR (numeric value)." };
    public static Option<string> TemplateRoot => new("--tmpRoot") { Description = "Set the template root directory." };
    public static Option<string> Title => new("--title") { Description = "The title for the ADR." };
    public static Option<bool> Verbose => new("--verbose") { Description = "Show the ADR's more information." };
    public static Option<bool> Silent => new("--silent", "-s") { Description = "Do not show status messages." };
}