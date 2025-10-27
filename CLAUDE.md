# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is adr-cli, a .NET 9.0 command-line tool for managing Architecture Decision Records (ADRs) and project planning tasks. The tool helps create, manage, and maintain structured documentation with dual markdown/JSON storage. It supports the Model Context Protocol (MCP) for integration with AI tools like Claude.

## Build and Development Commands

This is a .NET solution built with Visual Studio. All commands should be run from the `src/` directory:

```bash
# Build the solution
dotnet build adr.sln

# Run all tests
dotnet test

# Run tests with verbose output
dotnet test -v n

# Run a specific test
dotnet test --filter "FullyQualifiedName~AdrRecordRepository_CanWriteRecords"

# Run the CLI tool during development
dotnet run --project Adr.Cli/Adr.Cli.csproj -- [command] [options]

# Examples:
dotnet run --project Adr.Cli/Adr.Cli.csproj -- init
dotnet run --project Adr.Cli/Adr.Cli.csproj -- new "My Decision"
dotnet run --project Adr.Cli/Adr.Cli.csproj -- list

# Run as MCP server for AI tools
dotnet run --project Adr.Cli/Adr.Cli.csproj -- mcp

# Create release build
dotnet build -c Release

# Publish for specific runtime
dotnet publish Adr.Cli/Adr.Cli.csproj -c Release -r win-x64 --self-contained
dotnet publish Adr.Cli/Adr.Cli.csproj -c Release -r linux-x64 --self-contained
dotnet publish Adr.Cli/Adr.Cli.csproj -c Release -r osx-x64 --self-contained
```

## Architecture

The codebase follows a modular architecture with clear separation of concerns across 4 projects:

### Project Structure

1. **Adr.Cli** - Main CLI application with command handlers, domain models, and repositories
2. **McpCore** - Standalone MCP protocol implementation (JSON-RPC 2.0 and MCP models)
3. **Adr.Cli.UnitTests** - xUnit test suite with mocked dependencies
4. **CodeValidate** - Namespace validation utility

### Core Domain Models

The codebase supports two types of records, both extending `AdrRecordBase`:

**AdrRecord.cs** - Architecture Decision Records:
- Base fields: DateTime, FileName, RecordId, Title
- ADR-specific: Status (AdrStatus enum), SuperSedes, TemplateType, Context, Decision, Consequences, References
- Decision and Consequences marked `[JsonIgnore]` (stored only in .md files)
- Implements `ICloneable` for creating revisions

**TaskRecord.cs** - Project Planning Tasks:
- Base fields: DateTime, FileName, RecordId, Title
- Task-specific: DueDate, Status (PlanningStatus enum), Description, Details, Related, Logs
- PlanningStatus enum: None, New, OnHold, Planned, Active, Related, ReviewPending, ReviewComplete, AcceptancePending, Completed, Abandoned
- StatusUpdate tracking with justification logs

### Repository Pattern

**DocumentBasedRepository.cs** (abstract base):
- Generic file I/O operations for both ADRs and Tasks
- Handles .md (content) and .json (metadata) dual file approach
- Methods: CreateRootDocumentAsync, ReadContentAsync, GetFileInfoForRecord

**AdrRecordRepository.cs** (extends DocumentBasedRepository):
- Default path: `\docs\adr`, templates: `\docs\adr-templates`
- Key methods:
  - `WriteRecordAsync` - Creates .md and .json files
  - `ReadMetadataAsync` - Deserializes JSON metadata
  - `CopyRecordAsync` - Creates revision from existing ADR
  - `GetLayoutAsync` - Generates markdown from templates

**AdrTasksRepository.cs** (extends DocumentBasedRepository):
- Default path: `\docs\planning`
- Similar pattern for task management

### Configuration Management

**AdrSettings.cs** (implements IAdrSettings):
- Searches for `adr.config.json` by walking up directory tree
- Default paths: `\docs\adr`, `\docs\adr-templates`, `\docs\planning`
- Lazy folder creation on access
- File numbering system for sequential IDs
- Configuration format:
```json
{
  "path": "\\docs\\adr",
  "templates": "\\docs\\adr-templates",
  "tasks": "\\docs\\planning",
  "projectName": "Project Name"
}
```

### Command Structure

Commands follow a three-part pattern using System.CommandLine:

1. **I[Command].cs** - Interface defining async methods returning `Response` (from McpCore)
2. **[Command].cs** - Implementation with injected dependencies (IAdrSettings, ILogger, Repository, IStdOut)
3. **[Command]Setup.cs** - System.CommandLine command definition and registration

**Registered Commands** (in Program.cs):
- **ADR Commands**: init, sync, generate-toc, new, copy, query, list, link, unlink
- **Task Commands**: new-task, list-tasks, find-tasks, update-task, link-task, unlink-task, generate-task-toc

All handlers injected via DI and registered as singletons.

### Dual File Approach

Each record consists of two files with the pattern `{id:D5}-{title-slug}.{md|json}`:

**Markdown File** (human-readable content):
- Template-based with placeholders: `{RecordId}`, `{Title}`, `{Status}`, `{DateTime}`, etc.
- For ADRs: Status, Context, Decision, Consequences sections
- For Tasks: Status, Description, Details, Related tasks sections

**JSON Metadata File** (structured data):
- Serialized record object using System.Text.Json
- Contains all fields except those marked `[JsonIgnore]`
- Enables metadata search/queries without parsing markdown

**Synchronization**: The `UpdateFromMarkdown()` extension method parses markdown back to update metadata.

### MCP (Model Context Protocol) Integration

**Dual-Mode Execution** (Program.cs):
- CLI mode: `adr-cli [command]` - Traditional command-line interface
- MCP mode: `adr-cli mcp` or `adr-cli --mcp` - JSON-RPC server on stdin/stdout

**McpCore Project Structure**:
- `Server/McpServer.cs` - Base server implementation
- `JsonRpc/JsonRpcModels.cs` - JSON-RPC 2.0 protocol models
- `Protocol/McpProtocolModels.cs` - MCP-specific models
- `ToolCallRequest.cs`, `Response.cs` - Request/response wrappers

**AdrMcpServer.cs** - Implements 16 tools by delegating to command handlers

### Template System

**TemplateType enum**: Init, Ad (Architecture Decision), Asr (Architecture Significant Requirement), Revision, Task

Templates are stored in `docs/adr-templates/` and created on-demand when first used. Template placeholder substitution uses StringBuilder for efficiency.

### Dependency Injection Setup

All services registered as Singletons in Program.cs:

```csharp
// Logging (Debug: console, Release: warnings only)
ILogging (conditional on DEBUG/RELEASE)

// Core Services
IProcessHelper, ProcessHelper - Launch external editor
IStdOut, StdOutService - Mutable output service
IFileSystem, FileSystem - File system abstraction (System.IO.Abstractions)

// Configuration & Repositories
IAdrSettings, AdrSettings
IAdrRecordRepository, AdrRecordRepository
IAdrTasksRepository, AdrTasksRepository

// Command Handlers
IAdrInit, AdrInit
IAdrNew, AdrNew
IAdrQuery, AdrQuery
IAdrLink, AdrLink
IProjectPlanning, ProjectPlanning

// MCP Server
IMcpServer, AdrMcpServer
```

## Testing

The project uses xUnit with comprehensive mocking:

**Testing Stack**:
- **xUnit** - Test framework
- **Moq** - Mocking dependencies
- **System.IO.Abstractions** - File system abstraction for testable file operations
- **Custom XUnitLogger** - Captures ILogger output in test results

**Test Pattern** (example from AdrRecordRepositoryTests):
- Mock IFileSystem, IAdrSettings, IStdOut, ILogger
- Use in-memory streams for file I/O testing
- Verify UTF-8 encoding/decoding
- Test template generation, metadata serialization, file naming

**Run specific test**: `dotnet test --filter "FullyQualifiedName~TestMethodName"`

## Key Dependencies

- **System.CommandLine** - Command-line parsing and structure
- **Microsoft.Extensions.DependencyInjection** - Dependency injection container
- **Microsoft.Extensions.Logging** - Structured logging
- **System.IO.Abstractions** - File system abstraction for testability
- **System.Text.Json** - JSON serialization for metadata files

## MCP Integration

The tool can run as an MCP (Model Context Protocol) server to enable AI tools to interact with ADR repositories.

### Available MCP Tools

**16 tools total** - ADR tools (adr_init, adr_new, adr_list, adr_find, adr_link, adr_unlink, adr_copy, adr_sync, adr_generate_toc) and Task tools (task_new, task_list, task_find, task_update, task_link, task_unlink, task_generate_toc)

**Usage**: `adr-cli mcp` (listens on stdin/stdout for JSON-RPC)

### Claude Code Configuration

This repository includes `.claude/config.json` that automatically enables MCP integration:

```json
{
  "mcpServers": {
    "adr-cli": {
      "command": "adr-cli",
      "args": ["mcp"],
      "description": "Architecture Decision Records management tool"
    }
  }
}
```

For global access across all projects, add the same configuration to:
- **Windows**: `C:\Users\<username>\.claude\config.json`
- **macOS/Linux**: `~/.claude/config.json`

If `adr-cli` is not in PATH, use full path to executable in the `command` field.

## Key Behaviors

- Templates are created on-demand when first used
- Configuration auto-detected by walking up directory tree for `adr.config.json`
- Metadata synchronization via `UpdateFromMarkdown()` extension method
- File naming pattern: `{id:D5}-{title-slug}.{md|json}` (e.g., `00001-my-decision.md`)
- Revisions created via `CopyRecordAsync` with SuperSedes relationship
- Task status updates logged with DateTime and justification