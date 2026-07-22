# 00003. Implement task export command and MCP tool

2026-07-22

## Status

__New__

## Description

Per ADR 00008: export a local task to its configured external provider, creating a new work item or updating the existing one via stored sync metadata. Expose equivalent behavior through both a CLI command and an MCP tool per ADR 00005.

## Prerequisits

No prerequisits.

## Details

- Add a CLI command that accepts a local task identifier or path and exports the corresponding `TaskRecord`.
- Add an MCP tool with equivalent inputs and behavior, using the same application-level export service as the CLI command.
- Resolve the configured `ITaskSyncProvider` and external project or scope from the task’s sync metadata and applicable configuration.
- Create a new external work item when no external work-item ID is stored.
- Update the existing external work item when sync metadata contains an external work-item ID.
- After a successful creation, persist the returned provider, project or scope, and external work-item ID to the local task record so later exports perform updates rather than create duplicates.
- Do not modify local sync metadata when the provider operation fails.
- Return a clear result containing whether the work item was created or updated, its external identifier, and an external URL when supplied by the provider.
- Report actionable errors for missing tasks, incomplete provider configuration, unavailable providers, invalid sync metadata, authentication failures, and provider API failures. Ensure credentials and other secrets are never written to task files, command output, MCP responses, or logs.
- Keep provider-specific field mapping and API behavior inside the `ITaskSyncProvider` implementation rather than duplicating it in the CLI or MCP layers.
- Add tests covering create and update flows, metadata persistence, failure behavior, provider resolution, CLI argument handling, and MCP input/output handling.

This task depends on task 00001 for the provider abstraction and task sync metadata, and on task 00002 for Azure DevOps export support.


