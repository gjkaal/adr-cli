# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is adr-cli, a .NET 9.0 command-line tool for managing Architecture Decision Records (ADRs). The tool helps create, manage, and maintain ADR repositories with structured markdown and JSON metadata files. It also supports the Model Context Protocol (MCP) for integration with AI tools like Claude and Copilot.

## Build and Development Commands

This is a .NET solution built with Visual Studio. Common development commands:

```bash
# Build the solution (from src/ directory)
dotnet build adr.sln

# Run tests
dotnet test

# Build specific project
dotnet build Adr.Cli/Adr.Cli.csproj

# Run the CLI tool during development
dotnet run --project Adr.Cli/Adr.Cli.csproj -- [command] [options]

# Run as MCP server for AI tools
dotnet run --project Adr.Cli/Adr.Cli.csproj -- mcp

# Create release build
dotnet build -c Release

# Publish for specific runtime
dotnet publish Adr.Cli/Adr.Cli.csproj -c Release -r win-x64 --self-contained
```

## Architecture

The codebase follows a modular architecture with clear separation of concerns:

### Core Components
- **Program.cs**: Entry point with dependency injection setup, command registration, and MCP server mode
- **AdrRecord.cs**: Core domain model representing an ADR with metadata
- **AdrRecordRepository.cs**: Data access layer handling file I/O for ADR content and metadata
- **AdrSettings.cs**: Configuration management for ADR repository paths and settings

### MCP (Model Context Protocol) Support
- **McpCore/**: Separate project containing MCP protocol implementation
- **Mcp/AdrMcpServer.cs**: MCP server implementation for ADR operations
- **JsonRpc/**: JSON-RPC 2.0 protocol models and infrastructure
- **Protocol/**: MCP-specific protocol models and constants

### Command Structure
Commands are organized using System.CommandLine with separate handler and setup classes:
- **CommandHandlers/**: Contains business logic for each command (AdrInit, AdrNew, AdrQuery, AdrLink)
- **[Command]Setup.cs**: Command definition and registration logic
- **I[Command].cs**: Interfaces for command handlers

### Key Patterns
- **Dual file approach**: Each ADR consists of a .md file (content) and .json file (metadata)
- **Template system**: Customizable markdown templates in docs/templates/
- **Repository pattern**: AdrRecordRepository abstracts file system operations
- **Dependency injection**: Full DI container setup in Program.cs

### File Structure
- ADR documents stored in `docs/adr/` by default
- Templates in `docs/templates/`
- Configuration in `adr.config.json` at repository root
- Filenames follow pattern: `{id:D5}-{title-slug}.{md|json}`

## Testing

The project uses xUnit for testing with:
- **Moq** for mocking dependencies
- **System.IO.Abstractions** for file system abstraction (testable file operations)
- Test project: `Adr.Cli.UnitTests`

## Key Dependencies

- **System.CommandLine**: Command-line parsing and structure
- **Microsoft.Extensions.DependencyInjection**: Dependency injection
- **Microsoft.Extensions.Logging**: Structured logging
- **System.IO.Abstractions**: File system abstraction for testability
- **System.Text.Json**: JSON serialization for metadata

## MCP Integration

The tool can run as an MCP (Model Context Protocol) server to enable AI tools like Claude and Copilot to interact with ADR repositories:

### Available MCP Tools
- `adr_init`: Initialize new ADR repository
- `adr_new`: Create new Architecture Decision Record
- `adr_list`: List all ADRs with optional filtering
- `adr_find`: Search ADRs by text query
- `adr_link`: Link two ADRs with relationship
- `adr_unlink`: Remove links between ADRs
- `adr_copy`: Copy existing ADR to create new one
- `adr_sync`: Synchronize metadata with content
- `adr_generate_toc`: Generate table of contents

### Usage
```bash
# Start MCP server (listens on stdin/stdout for JSON-RPC)
adr-cli mcp
```

### Claude Code Configuration
This repository includes a `.claude/config.json` file that automatically configures the adr-cli tool as an MCP server for Claude Code:

```json
{
  "mcpServers": {
    "adr-cli": {
      "command": "adr-cli",
      "args": ["mcp"],
      "description": "Architecture Decision Records management tool for creating, managing, and maintaining ADRs and project planning tasks."
    }
  }
}
```

When working in this repository, Claude Code will automatically have access to all ADR management capabilities through the MCP protocol.

## Development Notes

- The tool supports both creating new ADRs and managing revisions/links between existing ones
- Templates are created on-demand when first used
- Metadata synchronization keeps JSON files in sync with markdown content
- The repository auto-detects configuration by walking up directory tree from current location
- MCP mode enables seamless integration with AI tools for automated ADR management
- memorize