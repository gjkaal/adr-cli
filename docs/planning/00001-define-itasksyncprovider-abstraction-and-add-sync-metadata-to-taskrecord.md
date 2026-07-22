# 00001. Define ITaskSyncProvider abstraction and add sync metadata to TaskRecord

2026-07-22

## Status

__New__

## Description

Per ADR 00008: introduce a provider-agnostic interface for exporting/importing tasks, and extend TaskRecord with sync metadata (provider, external project/scope, external work-item id) so exports can update existing work items instead of duplicating them.

## Prerequisits

No prerequisits.

## Details

- Define the `ITaskSyncProvider` interface and the provider-neutral contracts required for task import and export operations.
- Keep provider-specific SDK types and implementation details out of the abstraction and core task model.
- Extend `TaskRecord` with optional sync metadata for:
  - Provider identifier
  - External project or scope
  - External work-item identifier
- Ensure tasks without sync metadata remain valid and continue to represent unsynchronized local tasks.
- Make the metadata sufficient to distinguish between creating a new external work item and updating an existing one.
- Update relevant serialization, persistence, construction, and mapping logic to preserve the new fields.
- Add tests covering the interface contract and `TaskRecord` sync-metadata persistence, including records with no metadata and records linked to an external work item.


