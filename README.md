# Architecture Decision Toolset

Architecture for agile projects has to be described and defined differently. Not all decisions will be made at once, nor will all of them be done when the project begins. This command-line toolset (`adr-cli`, packaged as `n2adr`) helps create and manage Architecture Decision Records (ADRs) and project planning tasks, storing each record as a paired markdown/JSON file that is easy to review and diff in source control. It also runs as a Model Context Protocol (MCP) server so AI tools such as Claude can create, query, and update ADRs and tasks directly.

The ADR approach is based on the ideas of [Michael Nygard](https://cognitect.com/authors/MichaelNygard.html) and his post on [documenting architecture decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

## Functional areas

### Adr.Cli (`Adr.Cli`)

The main CLI application. It defines the domain models for both record types - `AdrRecord` (Status, Context, Decision, Consequences, SuperSedes, TemplateType) and `TaskRecord` (DueDate, Status, Description, Details, Related, status-update log) - and the repositories that read and write them, `AdrRecordRepository` and `AdrTasksRepository`, both built on a shared `DocumentBasedRepository` base that handles the dual `.md`/`.json` file format, template substitution, and file locking. Command handlers (`AdrInit`, `AdrNew`, `AdrQuery`, `AdrLink`, `ProjectPlanning`) implement the actual CLI commands and are exposed identically through the MCP server (`Mcp/AdrMcpServer.cs`), so a caller gets the same capabilities whether they use the CLI or an MCP tool call.

### Adr.Cli.Abstractions (`Adr.Cli.Abstractions`)

Interfaces and shared types with no implementation dependencies, safe to reference from any layer: `IAdrSettings`, `IAdrRecordRepository`, `IAdrTasksRepository`, `IProjectPlanning`, the `PlanningStatus`/`AdrStatus`/`TemplateType` enums, and the `IAdrProposalGenerator`/`ITaskProposalGenerator` interfaces used for AI-assisted drafting.

### Adr.Cli.Ai.AzureFoundry (`Adr.Cli.Ai.AzureFoundry`)

Drafts ADR and task content via an Azure AI Foundry chat model. `AzureFoundryProposalGenerator` fills in Context/Decision/Consequences for a new ADR; `AzureFoundryTaskProposalGenerator` fills in Description/Details for a new task. Both ground their prompt in the repository's existing records and its own markdown template, and both are best-effort - any failure (misconfiguration, auth, network) falls back to plain template-based creation rather than blocking the record from being created. See [AI-Setup.md](AI-Setup.md) for configuring a provider.

### McpCore (`McpCore`)

A standalone Model Context Protocol implementation: JSON-RPC 2.0 message types (`JsonRpc/JsonRpcModels.cs`), MCP protocol models (`Protocol/McpProtocolModels.cs`), and the base server loop used by `Adr.Cli`'s `AdrMcpServer` to expose ADR and task operations as MCP tools over stdio.

### CodeValidate (`CodeValidate`)

A small namespace-validation utility used during development to check that source files declare the namespace their folder location implies.

## Usage

Common commands (see `adr-cli --help` for the full list):

```bash
adr-cli init                    # create adr.config.json and the initial ADR
adr-cli new --title "..."       # create a new ADR (add --ai to draft Context/Decision/Consequences)
adr-cli new-task --title "..."  # create a new task (add --ai to draft Description/Details)
adr-cli generate-toc            # (re)generate docs/adr-toc.md - a table of every ADR
adr-cli task-toc                # (re)generate docs/tasks-toc.md - a table of every open task
adr-cli mcp                     # run as an MCP server over stdin/stdout
```

`docs/adr-toc.md` and `docs/tasks-toc.md` are generated files - run `generate-toc` / `task-toc` (or the equivalent `adr_generate_toc` / `task_generate_toc` MCP tools) after adding, linking, or changing the status of ADRs or tasks to keep them current.

## Project documentation

| Document | Description |
|----------|-------------|
| [CHANGELOG.md](CHANGELOG.md) | Full version history. |
| [AI-Setup.md](AI-Setup.md) | Configuring AI-assisted drafting for ADRs and tasks. |
| [docs/adr-toc.md](docs/adr-toc.md) | Table of contents for all ADRs. Regenerate with `adr-cli generate-toc`. |
| [docs/tasks-toc.md](docs/tasks-toc.md) | Table of contents for all open tasks. Regenerate with `adr-cli task-toc`. |

## Change log

See [CHANGELOG.md](CHANGELOG.md) for the full history. Current version: **1.0.0.10**.
