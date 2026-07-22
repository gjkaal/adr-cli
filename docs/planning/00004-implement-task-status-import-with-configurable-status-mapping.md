# 00004. Implement task status import with configurable status mapping

2026-07-22

## Status

__New__

## Description

Per ADR 00008: pull external work-item status and map it to a local task status via configurable provider mappings. An unknown or ambiguous external status must report the task as unsynchronized rather than guessing a local status. Expose through CLI and MCP.

## Prerequisits

No prerequisits.

## Details

- Use the task’s sync metadata introduced by task 00001 to identify the provider, external project or scope, and external work-item ID.
- Retrieve the current external work item through the configured `ITaskSyncProvider` implementation, including the Azure DevOps adapter from task 00002.
- Add configuration for mapping provider-specific external statuses to local task statuses.
- Normalize status values only according to explicitly supported configuration rules; do not infer mappings from similar names or workflow position.
- Apply the mapped status to the local task and persist the task file when exactly one valid local status is resolved.
- Treat the task as unsynchronized when:
  - no external work item can be resolved from its sync metadata;
  - the external status is missing or unknown;
  - no mapping is configured;
  - the mapping resolves to multiple local statuses; or
  - the configured local status is invalid.
- For unsynchronized results, preserve the existing local task status and return actionable diagnostics containing the provider and external status where available.
- Add a CLI command for importing status from the task’s configured external work item.
- Add an MCP tool with equivalent inputs, status-update behavior, and structured success or unsynchronized results.
- Ensure provider, authentication, transport, and configuration failures are reported without partially updating the local task.
- Add tests covering successful mappings, unknown and ambiguous statuses, invalid configuration, missing sync metadata, provider failures, unchanged local state on unsuccessful imports, and CLI/MCP behavior parity.
- Coordinate with task 00001 for the provider abstraction and sync metadata, task 00002 for Azure DevOps status retrieval, and task 00003 for consistent CLI and MCP conventions.


