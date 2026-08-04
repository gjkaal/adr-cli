# 00004. Implement task status import with configurable status mapping

2026-07-22

## Status

__New__

## Description

Per ADR 00008 (revised): pull external status and map it to a local task status via an explicit, independently-configured external-to-local status map (not derived by inverting the export map). An unknown or ambiguous external status must leave the task's sync state unmapped rather than guessing a local status. Named `task-import`/`task_import` (not "sync"). Expose through CLI and MCP.

## Prerequisits

Task 00001 (provider abstraction and sync metadata), task 00002 (Azure DevOps status retrieval), task 00003 (consistent CLI/MCP conventions), and task 00006 (provider-specific settings model).

## Details

- Use the task's `TaskSyncLink` (task 00001) to identify the provider, external scope, and external work-item ID.
- Retrieve the current external item's status through the configured `ITaskSyncProvider` implementation, including the Azure DevOps adapter from task 00002.
- Add configuration for an external-to-local status map, kept separate from the export's local-to-external map since external workflow states can be many-to-one against the coarser local `PlanningStatus` enum and the map is not safely invertible.
- Normalize status values only according to explicitly supported configuration rules; do not infer mappings from similar names or workflow position.
- Apply the mapped status to the local task and persist the task file when exactly one valid local status is resolved.
- This is a one-directional overwrite, not a merge: importing always overwrites the local status with whatever the external side currently resolves to, with no comparison against what changed locally since the last sync (no last-write-wins, no conflict detection - see ADR 00008 for why).
- Leave the task's sync state unmapped (task 00001) rather than guessing when:
  - no external work item can be resolved from its sync metadata;
  - the external status is missing or unknown;
  - no mapping is configured;
  - the mapping resolves to multiple local statuses; or
  - the configured local status is invalid.
- For unmapped results, preserve the existing local task status and return actionable diagnostics containing the provider and external status where available.
- Accept explicit task IDs or a `task-find`-style filter (see task 00003/00005 for the shared batch shape); with no filter, default to every task that already carries an external identifier for the currently active provider. Skip (report as skipped, not failed, not imported) any task whose sync link belongs to a different, no-longer-active provider - e.g. after a repository switches its configured connector.
- Add a CLI command for importing status from tasks' configured external work items.
- Add an MCP tool with equivalent inputs, status-update behavior, and structured success/unmapped/skipped results.
- Ensure provider, authentication, transport, and configuration failures are reported without partially updating the local task.
- Add tests covering successful mappings, unknown and ambiguous statuses, invalid configuration, missing sync metadata, provider failures, unchanged local state on unsuccessful imports, stale-provider-link skipping, default batch scope, and CLI/MCP behavior parity.


