using System.CommandLine;

namespace Adr.Cli.CommandHandlers;

public static class CommandOptions
{
    public static Option<string> AdrRoot => new("--adrRoot", "Set the adr root directory");
    public static Option<string> Context => new("--context", "Optional context for the ADR (otherwise a default value will be used)");
    public static Option<string> Filter => new("-q", "Only show an ADR if a word or words in used the ADR");
    public static Option<bool> IncludeContent => new("--full", "Search the full records (slow)");
    public static Option<string> Reason => new("--reason", "The reason for the link.");
    public static Option<string> Record => new("--record", "Synchronize only for a single record");
    public static Option<bool> Requirement => new(new[] { "--req", "-q" }, "The ADR is a critical requirement.");
    public static Option<string> Revision => new("--rev", "The ADR rivision for an earlier ADR, provide a valid id.");
    public static Option<bool> SortReverse => new("--desc", "Show the ADR's with the latest ADR first");
    public static Option<string> SourceId => new("--source", "The source ADR");
    public static Option<string> StartAt => new("--startAt", "Synchronize from this record until the end");
    public static Option<string> TargetId => new("--target", "The target ADR");
    public static Option<string> TemplateRoot => new("--tmpRoot", "Set the template root directory");
    public static Option<string> Title => new("--title", "The title for the ADR");
    public static Option<bool> Verbose => new("--verbose", "Show the ADR's more information");
    public static Option<bool> Silent => new(new[] { "-s", "--silent" }, "Do not show status messages");
}