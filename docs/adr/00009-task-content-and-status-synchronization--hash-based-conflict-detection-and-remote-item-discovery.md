# 00009. Task content and status synchronization: hash-based conflict detection and remote-item discovery

2026-08-04

## Status

__Final__

Extends and partially supersedes ADR 00008's content-import restriction [00008.Export tasks and import task status updates to and from external applications (e.g. Azure DevOps)](.\00008-export-tasks-and-import-task-status-updates-to-and-from-external-applications-(e.g.-azure-devops))

## Context

ADR 00008 established `task-export`/`task-import` as one-directional, non-merging operations: export always overwrites the external item's title/description/details from local, and import only ever pulls status, never content - "changes made externally to titles, descriptions, or other unsupported fields are not imported and may be overwritten by a later export." That protected against accidental overwrites by simply never attempting the risky direction.

In practice this is too conservative. External content edits (a description tweaked directly on the GitHub Projects board) are common and legitimate, and a blind local-wins export silently discards them. At the same time, a truly bidirectional merge (as rejected in ADR 00008) is still not wanted - there is no general reconciliation engine, and a genuine conflict must never be resolved by guessing.

Separately, tasks are not the only way items end up on a project board: a teammate may add a task directly on GitHub (as a Draft Issue or, once promoted, a real repository Issue) without ever going through `task-export`. Those items had no path into this repository at all under ADR 00008's design.

## Decision

Content (title + description/details) now flows in both directions, but only ever automatically in the direction that is provably safe, detected via a hash marker rather than timestamps or manual conflict flags:

- On every successful export, the pushed content's combined hash (SHA256 of `Title` and the provider's body field, all whitespace characters stripped before hashing so purely cosmetic differences never register as a change) is appended as a trailing `[HASH:<hash>]` line on the **remote** copy only. Local files never carry this line - the local hash is always computed fresh from whatever the file currently contains at sync time, never cached, since the file may be edited by any other process between syncs.
- On `task-import` (now always attempted alongside the existing status pull, not a separate opt-in), the remote body's trailing marker is compared against the current local hash:
  - Local hash matches the marker and remote content is unchanged → nothing to do.
  - Local hash matches the marker but remote content differs → local hasn't moved since the last sync, remote has - safe to adopt remote's title/description locally, then immediately push again to refresh the marker to the new baseline.
  - Local hash does not match the marker → local moved since the last sync, so the safety guarantee is gone regardless of what remote did - refuse to touch either side and record the task's sync state as `Mismatch` (a new value alongside `Synced`/`Unmapped` on `TaskSyncLink.SyncState`) for manual reconciliation.
- `task-export` now performs the symmetric check before overwriting an existing item: it fetches the remote content first and verifies the remote's own current content still hashes to its own embedded marker (i.e. nobody touched it since the last sync). If it does, export proceeds and refreshes the marker; if it doesn't, export refuses and reports `Mismatch` rather than silently discarding the untracked remote edit. A brand-new export (no existing link) has no baseline to check and always proceeds.
- `task-export --force` (CLI), `force` (MCP `task_export`) bypasses this check for a single task, overwriting the remote item with local content regardless - a deliberate, explicit override for when a person has already reviewed both sides and decided local should win, rather than resolving a `Mismatch` by hand.
- `--dry-run` (CLI, both commands), `dryRun` (MCP, both tools) computes and reports the same outcome a real run would - create/update/mismatch, status mapping, content pull, newly discovered tasks - performing every read needed to detect divergence, but skipping every write, local or remote. A dry-run export of a brand-new task reports an empty external id, since nothing was actually created to report an id for. A dry-run import performs discovery and reports what it would adopt, but creates no local file and never re-exports to refresh a marker.
- The hash/marker mechanics (`ContentSyncMarker.ComputeHash`/`AppendMarker`/`SplitMarker`/`ResolveImport`, and the shared `TaskBodyFormat.Combine`/`Split` used to fold `Description`+`Details` into one body field and split a pulled body back out) live in `Adr.Cli.Abstractions` so every provider - GitHub Projects today, Azure DevOps later - uses identical logic; a marker written by one provider implementation must round-trip correctly regardless of which provider reads it back.
- `task-import`'s default scope (no `--id`/`--filter`) is extended beyond "every task with a sync link for the active provider": it also calls a new `ITaskSyncProvider.DiscoverItemsAsync()` to list every item currently on the board, and adopts any item that has no content-sync marker yet (never pushed through this tool) and whose title doesn't match an existing local task. An adopted item becomes a brand-new local task, linked, pushed once to establish a valid marker, and has its current status imported immediately so it doesn't sit at the default "New" status until a second run. A discovered item whose title *does* match an existing local task is left alone rather than guessing a link.
- For GitHub Projects specifically, reading title/description/status now works for any item type on the board - draft issue, real issue, or pull request - not just draft issues this connector created, since a draft issue promoted to a real issue keeps the same stable project-item id and should keep syncing under the same local task. Writing content, however, still only works for draft issues (`updateProjectV2DraftIssue`); a linked real issue/PR has its content left alone (status still pushes normally, since field updates work on any item type) because updating a real issue's body requires repository write permissions this connector deliberately doesn't request (see ADR 00008).

## Consequences

Content edits made directly on the external board are no longer silently lost on the next export - they are either safely pulled in (when local hasn't also changed) or surfaced as a `Mismatch` for a person to resolve, never guessed. This supersedes ADR 00008's Consequences statement that external content edits "may be overwritten by a later export"; that is now true only when local has also changed since the last sync (a genuine conflict) or when `--force` is used deliberately.

The whitespace-insensitive hash means reformatting alone (line endings, trailing spaces) never trips a false mismatch, but it also means a whitespace-only remote edit is invisible to this mechanism - not a concern in practice, since whitespace-only changes carry no real content.

Every `task-export` on an existing link now requires a content read before the write (to check the remote's self-consistency), where previously it was a blind write - one additional round trip per export, accepted for the correctness gained. `task-import`'s default scope also now makes a full board listing call to support discovery, in addition to the per-task reads it already made - a second source of added latency, and, for large boards, an unbounded read (the discovery query is not paginated past the first 100 items) that is a known limitation rather than a solved case.

The `Mismatch` state adds a third outcome (alongside `Succeeded`/`Unmapped`) to every batch report, and a `Skipped` case remains for stale-provider links per ADR 00008 - batch reporting now needs to communicate four distinct non-identical outcomes clearly, which places more weight on `task-export`/`task-import`'s per-item message text than before.

Automatically adopting board-only items as new local tasks means `task-import`'s default scope can now create files, not just update them - a behavior change from ADR 00008, where import only ever touched tasks that already existed locally. Title-based matching against existing local tasks is a heuristic, not a guaranteed-correct identity check; a coincidental title match silently prevents adoption (treated as "already represented locally") rather than creating a duplicate, which is the safer failure mode of the two but is still a known limitation, not a solved one.

`--force` is a deliberate escape hatch outside the safety mechanism this ADR otherwise builds - using it on more than one task at a time reintroduces exactly the blind-overwrite risk ADR 00008 originally accepted and this ADR set out to reduce, so it is intended for single-task, human-reviewed use, not batch operations, though nothing currently prevents the latter beyond a logged warning.

