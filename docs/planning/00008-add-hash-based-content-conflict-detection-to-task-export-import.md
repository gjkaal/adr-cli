# 00008. Add hash-based content conflict detection to task export/import

2026-08-04

## Status

__Completed__

## Description

Per ADR 00009: ContentSyncMarker hash/marker mechanics, TaskSyncState.Mismatch, symmetric export/import divergence checks, --force override.

## Prerequisits

Task 00001 (provider abstraction and sync metadata), task 00006 (provider settings model).

## Details

- Added `ContentSyncMarker` (Adr.Cli.Abstractions/Sync): whitespace-stripped combined Title+Body SHA256 hash, `[HASH:...]` marker append/split, and `ResolveImport` classifying NoChange/Pull/Mismatch. Shared so every provider agrees on the same algorithm.
- Added `TaskBodyFormat.Combine`/`Split` (shared, replacing the GitHub provider's private CombineBody) so a pulled body can be split back into Description/Details using the same separator convention export used to join them.
- Added `TaskSyncState.Mismatch` (and `TaskSyncItemOutcome.Mismatch` for batch reporting), values chosen so the pre-existing zero-value/JSON-omission behavior for `Unmapped` still round-trips correctly.
- `GitHubProjectsTaskSyncProvider.ExportAsync`: on an existing link, fetches remote content first, checks the remote's own content still hashes to its own embedded marker before overwriting; refuses and returns `Mismatch` if not (unless `force` is passed, which skips the check).
- `GitHubProjectsTaskSyncProvider.ImportAsync`: fetches remote content alongside status, resolves via `ContentSyncMarker.ResolveImport`, returns `PulledTitle`/`PulledBody` on a safe pull or `Mismatch` when local has diverged - status is not read at all when a mismatch is detected.
- `ProjectPlanning.ExportOneAsync`/`ImportOneAsync`: persist `Mismatch` onto `TaskSyncLink.SyncState` without advancing `LastSyncedAt`; apply pulled content to the local task and immediately re-export to refresh the marker baseline.
- Added `--force` (CLI `task-export`) / `force` (MCP `task_export`), threaded through `IProjectPlanning.ExportTasksAsync` and `ITaskSyncProvider.ExportAsync`; logs a warning if used with more than one task selected.
- Tests: `Adr.Cli.Sync.GitHubProjects.UnitTests` covers safe update, remote-diverged mismatch, real-issue content-push skip; `Adr.Cli.UnitTests` covers mismatch persistence, force override, safe pull + re-export, via a fake `ITaskSyncProvider`.


