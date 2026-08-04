# 00003. Implement task export command and MCP tool

2026-07-22

## Status

__New__

## Description

Per ADR 00008 (revised): export local tasks to the currently configured connector, creating a new external item or updating the existing one via stored sync metadata, and pushing local status via an explicit local-to-external status map. Named `task-export`/`task_export` (not "sync", which already refers to the unrelated markdown-metadata resync feature). Expose equivalent behavior through both a CLI command and an MCP tool per ADR 00005.

## Prerequisits

Task 00001 (provider abstraction and sync metadata), task 00002 (Azure DevOps export support), and task 00006 (provider-specific settings model) for provider resolution.

## Details

- Add a `task-export` CLI command and a `task_export` MCP tool with equivalent inputs and behavior, both using the same application-level export service (`ITaskExport`/`TaskExport` following this repo's three-part command handler pattern).
- Accept either explicit task IDs or a `task-find`-style filter, so a single invocation can target one task or many (batch export) - see task 00005 for per-task batch reporting.
- Resolve the single currently-configured `ITaskSyncProvider` (task 00006); when none is configured, report clearly rather than silently no-op per task.
- Create a new external item when no external work-item ID is stored for the active provider; update the existing one when sync metadata already contains one.
- Push the local `PlanningStatus` to the external side using the export's local-to-external status map. A local status absent from that map leaves the task's sync state unmapped (task 00001) rather than guessing an external value.
- After a successful creation, persist the returned provider, scope, and external identifier to the local task record so later exports perform updates rather than create duplicates within a single clone. Duplicate external items from two clones exporting the same task before either has the other's sync metadata is a documented, unsolved limitation of ADR 00008 - do not attempt to detect or prevent it here.
- Do not modify local sync metadata when the provider operation fails.
- Return a clear result containing whether the item was created or updated, its external identifier, and an external URL when supplied by the provider.
- Report actionable errors for missing tasks, incomplete provider configuration, no active provider, invalid sync metadata, authentication failures, and provider API failures. Ensure credentials and other secrets are never written to task files, command output, MCP responses, or logs.
- Keep provider-specific field mapping and API behavior inside the `ITaskSyncProvider` implementation rather than duplicating it in the CLI or MCP layers.
- Add tests covering create and update flows, status push, metadata persistence, failure behavior, provider resolution, batch/filter handling, CLI argument handling, and MCP input/output handling.


