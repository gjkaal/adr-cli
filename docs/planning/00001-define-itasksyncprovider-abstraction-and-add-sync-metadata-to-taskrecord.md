# 00001. Define ITaskSyncProvider abstraction and add sync metadata to TaskRecord

2026-07-22

## Status

__New__

## Description

Per ADR 00008 (revised): introduce a provider-agnostic interface for exporting/importing tasks, and extend TaskRecord with sync metadata (a `TaskSyncLink` per task: provider, external scope, external work-item id, sync state, last-synced timestamp) so exports can update existing work items instead of duplicating them, and so unmapped statuses are recorded without guessing.

## Prerequisits

No prerequisits.

## Details

- Define the `ITaskSyncProvider` interface and the provider-neutral contracts required for task import and export operations, including pushing a local status out (export) and pulling an external status in (import) - both directions are explicit, one-directional operations with no automatic merge (see task 00003/00004).
- Keep provider-specific SDK types and implementation details out of the abstraction and core task model.
- Add a `TaskSyncLink` type and a `List<TaskSyncLink>` field directly on `TaskRecord` (not a sidecar file or external store), holding:
  - Provider identifier
  - External scope (recorded for traceability; scope itself is a single global per-repository config value, not settable per task - see task 00006)
  - External work-item identifier
  - Sync state (e.g. synced / unmapped) - tracks whether the last export/import could resolve a status mapping, kept separate from `PlanningStatus` so sync health never contaminates the task's actual workflow state
  - Last-synced timestamp
- Ensure tasks without sync metadata remain valid and continue to represent unsynchronized local tasks.
- Make the metadata sufficient to distinguish between creating a new external work item and updating an existing one.
- Update `TaskRecord.Clone()`, serialization, persistence, construction, and mapping logic to preserve the new field.
- Add tests covering the interface contract and `TaskRecord` sync-metadata persistence, including records with no metadata, records linked to an external work item, and records with an unmapped sync state.


