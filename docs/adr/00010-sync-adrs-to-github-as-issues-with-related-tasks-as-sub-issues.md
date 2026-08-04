# 00010. Sync ADRs to GitHub as Issues with related Tasks as sub-issues

2026-08-04

## Status

__Proposed__

## Context

ADR 00008 and ADR 00009 established task-export/task-import against GitHub Projects (v2), deliberately scoped to Draft Issues so the PAT only ever needs project-scope write, never repository write. That was a conscious boundary, not an oversight.

This ADR extends the same repository into a second dimension: syncing ADRs themselves to GitHub, as real repository-backed Issues rather than Draft Issues, with each ADR's related Tasks attached as GitHub sub-issues of that Issue (using the now-GA addSubIssue/removeSubIssue/reprioritizeSubIssue GraphQL mutations). GitHub's sub-issue relationship only links Issue-to-Issue - a Draft Issue cannot be a sub-issue of anything - so this is a genuine, deliberate increase in required PAT scope (project write -> project + repository write), not something that can be avoided while still delivering sub-issue linking.

AdrRecord has no sync-link field today, unlike TaskRecord.SyncLinks. AdrRecord's content shape also differs from Task's: Title + Context + Decision + Consequences (four fields) versus Task's Title + Description + Details (two fields), so TaskBodyFormat's combine/split cannot be reused as-is. AdrStatus (New/Proposed/Final/Accepted/Error/Obsolete) is also a different vocabulary from PlanningStatus, and doesn't map naturally onto Issue open/closed state or onto the task board's existing "Status" field options.

The command surface has a naming constraint already established by ADR 00008: the word "sync" is reserved in this CLI for the existing markdown-to-metadata resynchronization commands (adr sync / adr_sync), so the new GitHub-facing commands must be named differently, mirroring task-export/task-import.

This ADR documents the resolved design after interviewing the user on the open questions the handoff task (planning/00010) deliberately left unresolved: task promotion, permission scope, type reuse, and status mapping.

## Decision

New CLI commands `adr-export`/`adr-import` and matching MCP tools `adr_export`/`adr_import` are added, mirroring `task-export`/`task-import`'s naming and semantics rather than reusing the word "sync", which ADR 00008 already reserved for the unrelated markdown-to-metadata resynchronization commands (`adr sync`/`adr_sync`).

Both commands reuse the existing `sync` section of `adr.config.json` and its PAT (`sync.syncPatName`, default env var unchanged) rather than introducing a separate provider section or credential - there is still only one active connector per repository. This means the single PAT now needs a broader scope: project write (as today) plus repository/issue write, going forward, for any repository that configures ADR sync. `GitHubProjectsSettings` gains three fields to support this: `TargetRepository` (owner/repo, required for creating and promoting real Issues), `AdrStatusFieldName` (default `"ADR Status"`, a single-select field on the same project board, kept deliberately separate from the existing `StatusFieldName` used by tasks so the two status vocabularies never mix in one field's option list), and `AdrImportStatusMap`/`AdrExportStatusMap` (`Dictionary<string, AdrStatus>` / `Dictionary<AdrStatus, string>`), configured independently of each other and of the task status maps, following the same non-inverted, unmapped-rather-than-guessed pattern as task sync.

`TaskSyncLink`/`TaskSyncState` are renamed to `SyncLink`/`SyncState` (dropping the `Task` prefix) in `Adr.Cli.Abstractions/Sync`, and become the shared sync-link type for both record kinds: `TaskRecord.SyncLinks` keeps using it, and `AdrRecord` gains its own `SyncLinks` field of the same type. `TaskSyncBatchResult`/`TaskSyncItemResult`/`TaskSyncItemOutcome` are renamed the same way, to `SyncBatchResult`/`SyncItemResult`/`SyncItemOutcome`, since batch/per-item reporting is equally record-kind-agnostic and `adr-export`/`adr-import` reuse it rather than defining a parallel reporting type. This is a mechanical rename with no behavioral change - `SyncState.Unmapped` remains the zero value for the same reason it did as `TaskSyncState.Unmapped`. A new `IAdrSyncProvider` interface is added alongside `ITaskSyncProvider`, mirroring its shape (`ExportAsync`/`ImportAsync`/`DiscoverItemsAsync` against `AdrRecord`) rather than generalizing `ITaskSyncProvider` itself over both record kinds, since the two interfaces' export behavior diverges (sub-issue attachment, described below, has no task-side equivalent). `Adr.Cli.Sync.GitHubProjects` gains a new `GitHubProjectsAdrSyncProvider` implementing it, alongside the existing `GitHubProjectsTaskSyncProvider`, both sharing the same `IGitHubGraphQlClient`.

Content sync reuses `ContentSyncMarker` unchanged (hash/marker mechanics are already provider- and shape-agnostic), but ADRs need their own combine/split logic: a new `AdrBodyFormat` (alongside `TaskBodyFormat`) combines `Context`+`Decision`+`Consequences` under markdown section headers into the Issue body that gets hashed and marker-stamped, and does a best-effort header-based split back on import - write-only concatenation is acceptable and a body that doesn't preserve the section headers is a known limitation, not a solved case, exactly as ADR 00009 already accepts for `TaskBodyFormat`.

Neither `TaskRecord.Related` (task-to-task only) nor `AdrRecord.References` (ADR-to-ADR only) can express an ADR-to-Task relationship today - both are keyed by a bare record id, and Tasks and ADRs each number their own records from 1, so a shared dictionary can't disambiguate "ADR 5" from "Task 5". `AdrRecord` therefore gains a new `RelatedTasks` field (`Dictionary<int, string>`, task id -> remark), mirroring the existing `References`/`Related` pattern but explicitly scoped to Task ids. Two new one-directional commands, CLI `adr-link-task`/`adr-unlink-task` and MCP `adr_link_task`/`adr_unlink_task`, populate and clear it - metadata-only, no markdown content edit, mirroring `task-link`/`task-unlink`'s simpler pattern rather than `adr-link`'s (which also inserts a line under the ADR's Status section, not needed here since the sub-issue relationship on GitHub itself is the visible artifact of this link, not ADR markdown).

Each ADR's related Tasks (via the new `AdrRecord.RelatedTasks`) are attached as GitHub sub-issues of the ADR's Issue, cascading automatically as part of `adr-export` rather than requiring a separate promotion step. For each related task, `adr-export`:

- creates a new repository-backed Issue directly in `TargetRepository` (not a Draft Issue) if the task has no external item yet, then adds that Issue to the configured project board as an item so the task's existing status-field export/import keeps working;
- promotes an existing Draft Issue in place via `convertProjectV2DraftIssueItemToIssue` targeting `TargetRepository` if the task's current link is a Draft Issue - this preserves the project-item id, so the task's existing `SyncLink`/content marker stays valid across the promotion;
- reuses the task's existing Issue/PR link as-is if it's already promoted;

and then attaches it under the ADR via `addSubIssue(issueId: <ADR's Issue node id>, subIssueId: <task's Issue node id>)` (GitHub's now-GA sub-issue relationship; `removeSubIssue`/`reprioritizeSubIssue` exist but are not wired up by this decision). Promotion is one-way and `adr-export`-only: `task-export` never promotes a Draft Issue on its own, even for a task related to an ADR, until that ADR is actually exported; nothing in this decision ever demotes a promoted task back to a Draft Issue.

Because `adr-export`'s PAT carries repository write (unlike plain task sync), ADR 00009's stated limitation - "a linked real issue/PR has its content left alone... because updating a real issue's body requires repository write permissions this connector deliberately doesn't request" - no longer applies to a task once it has been promoted through this path: `task-export`/`task-import` write a promoted task's content via a real-Issue-update mutation instead of `updateProjectV2DraftIssue`. Unpromoted tasks, or tasks in repositories that never configure ADR sync, are unaffected and keep ADR 00009's original behavior.

Any CLI/MCP input that accepts a GitHub display number (an issue/PR number as seen in a URL, e.g. the reference item `223326914`) is resolved to a GraphQL node id via `GET /repos/{owner}/{repo}/issues/{number}` (REST) before use, since GitHub's GraphQL API exposes no by-number lookup - only `node(id:)` and, separately, project-item listing.

## Consequences

The PAT named by `sync.syncPatName` must be upgraded to include repository/issue write scope the moment a repository configures ADR sync; repositories that only ever use `task-export`/`task-import` can keep the narrower project-only scope they have today. This is a deliberate, accepted scope increase, not an oversight - the alternative (a second PAT/config section) was considered and rejected to keep one active connector's configuration in one place.

The `TaskSyncLink`/`TaskSyncState`/`TaskSyncBatchResult`/`TaskSyncItemResult`/`TaskSyncItemOutcome` → `SyncLink`/`SyncState`/`SyncBatchResult`/`SyncItemResult`/`SyncItemOutcome` rename touches every existing reference in `Adr.Cli.Sync.GitHubProjects`, `Adr.Cli.Sync.GitHubProjects.UnitTests`, `Adr.Cli` (`ProjectPlanning.cs`, `ProjectPlanningSetup.cs`, `AdrMcpServer.cs`), and `Adr.Cli.UnitTests/CommandHandlers/WithProjectPlanning.cs` - mechanical and behavior-preserving, but it must land as one commit so the solution never sits half-renamed. `AdrRecord.SyncLinks` and `AdrRecord.RelatedTasks` are new fields; existing ADR JSON files predating this change simply omit them and deserialize to empty collections, consistent with `Constants.JsonOptions` omitting default-valued properties.

Because `AdrStatusFieldName` is a dedicated field, ADRs must be added as project-board items in addition to being real repository Issues - additive, not conflicting, since a repository Issue can simultaneously be a project item.

Cascading task promotion means a single `adr-export` invocation can fan out into far more external writes than one task-export batch (the ADR itself plus every not-yet-promoted related task) - per-item outcome reporting (`Succeeded`/`Unmapped`/`Mismatch`/`Skipped`/`Failed`, per ADR 00009) matters more here, not less, since one command now drives many external mutations at once. Task promotion is irreversible by design: once a task becomes a real Issue there is no path in this decision back to a Draft Issue.

`AdrBodyFormat`'s section split, like `TaskBodyFormat`'s, is best-effort on import - an externally-edited Issue body that doesn't preserve the `Context`/`Decision`/`Consequences` headers can't be split back cleanly. This is accepted as a known limitation rather than solved, matching the precedent ADR 00009 already set for task content.

The exact current requirements for the sub-issue mutations (whether a `GraphQL-Features` preview header is still needed post-GA, precise `AddSubIssueInput` shape) were not fully confirmed against GitHub's documentation at the time this ADR was written - the documentation pages available were inconclusive. Implementation must verify directly against the live GraphQL schema/API (e.g. an introspection query through the existing `IGitHubGraphQlClient`) before relying on any assumed shape; this is a research gap carried forward, not a decided detail.

`adr-export`/`adr-import` are additive to the CLI/MCP surface (maintaining ADR 00005 parity) and do not change the existing `adr sync`/`adr_sync` markdown-resynchronization commands in any way.
