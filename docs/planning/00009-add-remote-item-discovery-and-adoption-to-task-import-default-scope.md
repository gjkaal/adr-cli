# 00009. Add remote-item discovery and adoption to task-import default scope

2026-08-04

## Status

__Completed__

## Description

Per ADR 00009: ITaskSyncProvider.DiscoverItemsAsync, title-matching against local tasks, auto-create+link+push for unmatched board items, GitHub Issue/PullRequest content support alongside DraftIssue.

## Prerequisits

Task 00008 (content conflict detection - discovery reuses `ExportOneAsync`/`ImportOneAsync` and the marker mechanics).

## Details

- Added `DiscoveredExternalItem` and `ITaskSyncProvider.DiscoverItemsAsync()`; `GitHubProjectsTaskSyncProvider` implements it via a project `items` GraphQL query, returning every item's title/body/marker-presence regardless of content type.
- Broadened `FetchItemContentAsync` (used by both export's pre-check and import) and discovery to read `DraftIssue`, `Issue`, and `PullRequest` content via `__typename`-discriminated inline fragments, since a draft issue promoted to a real issue keeps the same stable `ProjectV2Item` id and must keep syncing under the same local task. Writing content remains draft-issue-only (`updateProjectV2DraftIssue`) - a linked real issue/PR pushes status only, since content writes need repository permissions this connector doesn't request.
- Added `TaskSyncLink.ExternalContentType`/`ExternalUrl`, persisted on every export/import, so a promotion is visible in local task metadata without a separate lookup.
- `ProjectPlanning.ImportTaskStatusAsync`'s default scope (no `--id`/`--filter`) now also calls `DiscoverAndAdoptNewTasksAsync`: lists board items, skips ones already carrying a marker or matching an existing local task's title (case-insensitive), and adopts the rest - creates a new local `TaskRecord`, links it, pushes once to establish a marker, then immediately imports its current status so it doesn't sit at "New" until a second run.
- Tests: `Adr.Cli.Sync.GitHubProjects.UnitTests` covers discovery returning marker-flagged items across content types; `Adr.Cli.UnitTests` covers adoption creating a new task and skipping a title-matching one, via a fake provider.


