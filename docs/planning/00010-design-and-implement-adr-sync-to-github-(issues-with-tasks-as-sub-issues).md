# 00010. Design and implement ADR sync to GitHub (Issues with tasks as sub-issues)

2026-08-04

## Status

__New__

## Description

Sync ADRs to GitHub as real Issues (not Draft Issues), with the repository's tasks linked as sub-issues of their related ADR. Not yet designed - this task is the handoff/starting point for a fresh session.

## Prerequisits

Read ADR 00008 and ADR 00009 first - they establish the design philosophy and infrastructure this
task extends. Do not re-derive these from scratch; reuse what's already built where it applies.

## Details

### Goal

ADRs should sync to GitHub as real Issues (not Draft Issues), with the repository's Tasks linked as
sub-issues of their related ADR. Not yet designed - no code exists for this yet. This task exists so
a fresh session has everything needed to start without the prior session re-explaining it.

### What already exists (task sync - reuse, don't rebuild)

- `Adr.Cli.Abstractions/Sync/`: `ContentSyncMarker` (whitespace-stripped combined-hash + `[HASH:...]`
  marker embed/strip/compare), `TaskBodyFormat` (Description+Details combine/split), `TaskSyncLink`
  + `TaskSyncState` (`Unmapped`/`Synced`/`Mismatch` - note `Unmapped` is deliberately the zero value
  because `Constants.JsonOptions` omits properties equal to their type's default), `ITaskSyncProvider`
  (`ExportAsync`/`ImportAsync`/`DiscoverItemsAsync`, all with `force`/`dryRun` params), 
  `DiscoveredExternalItem`, `TaskSyncBatchResult`/`TaskSyncItemOutcome`
  (`Succeeded`/`Unmapped`/`Mismatch`/`Skipped`/`Failed`), `TaskSyncProviderSettings` (opaque
  per-provider `settings` blob + configurable `syncPatName`).
- `Adr.Cli.Sync.GitHubProjects`: `GitHubProjectsTaskSyncProvider`, `GitHubGraphQlClient`
  (raw HTTP GraphQL, `IGitHubGraphQlClient` is injectable so tests use a fake instead of a real call),
  `GitHubProjectsSettings`.
- `Adr.Cli/CommandHandlers/ProjectPlanning.cs`: `ExportTasksAsync`/`ImportTaskStatusAsync`
  orchestration - batch selection by id/filter, per-item outcome + message, `force`, `dryRun`,
  discovery/adoption of board-only items, marker persistence.
- Config: `adr.config.json`'s `sync` section (`provider`, `syncPatName`, opaque `settings`).
  Credentials via PAT env var, default `ADR_CLI_SYNC_PAT`, overridable per-config. Same pattern
  exists for AI (`ai.apiKeyName`, default `ADR_CLI_AI_API_KEY`).
- CLI: `task-export`/`task-import` (`--id`, `-q`/`--filter`, `--force`, `--dry-run`). MCP:
  `task_export`/`task_import` mirroring the CLI, with full parameter descriptions in
  `AdrMcpServer.cs` - keep tool/parameter descriptions accurate as this feature grows; they were
  found stale once already this project (see ADR 00009's task-import description drift) and had to
  be corrected in a dedicated review pass.
- Tests: `Adr.Cli.Sync.GitHubProjects.UnitTests` (provider-level, fake `IGitHubGraphQlClient`),
  `Adr.Cli.UnitTests/CommandHandlers/WithProjectPlanning.cs` (orchestration-level, fake
  `ITaskSyncProvider`). Both suites are the reference pattern to follow for ADR sync's own tests.
- Design philosophy established across ADR 00008/00009, carry it forward rather than relitigating:
  local stays authoritative; automatic sync only proceeds when provably safe (hash marker); a real
  conflict becomes `Mismatch`, never guessed; `--force` is a deliberate, single-item escape hatch;
  `--dry-run` performs every read but no write; provider settings are opaque per-provider blobs, not
  a fixed shared schema, because different providers scope themselves too differently to share one.

### Known open questions / blockers surfaced but not resolved

These came up when this was originally discussed - do not treat them as decided, they need fresh
design (grill/clarify with the user before writing code, matching how ADR 00008/00009 were built):

1. **Real Issues need repository write permissions.** Task sync deliberately avoided this (Draft
   Issues only need project-scope write). ADR sync as real Issues is a genuine permission increase -
   likely needs its own `targetRepository` (owner/repo) setting distinct from the project-scoped
   `owner`/`projectNumber` used for tasks, and the PAT needs `repo`/issue-write scope. Decide whether
   this reuses the same `sync` section/PAT or needs its own.
2. **The reference item's id is very likely not a GraphQL node id.** The user created a draft titled
   "Record Architecture Decisions initialization" and gave its id as `223326914` - that number has
   the shape of GitHub's legacy/display numeric id (as seen in UI URLs), not the base64 `id` field
   `node(id: ...)` queries require. Don't assume it's directly usable; verify how to resolve it
   (e.g. list project items and match by title, or find whichever GraphQL path resolves a numeric
   id/URL to a node) before building around it.
3. **GitHub's sub-issue feature/API needs fresh research.** It's a relatively new GitHub feature
   (issue parent/child relationships) - confirm the current GraphQL mutation(s) (e.g. `addSubIssue`),
   any required preview/feature flags, and whether it's GA, since this project's existing knowledge
   of the GitHub API may be stale by the time this is picked up.
4. **`AdrRecord` has no sync-link field at all today** (unlike `TaskRecord.SyncLinks`). Decide
   whether to reuse `TaskSyncLink`/`TaskSyncState` as-is (possibly renaming away from the `Task`
   prefix if it's shared between ADRs and Tasks), or define a parallel ADR-specific type. Don't
   assume - this is a real naming/reuse decision.
5. **Content shape differs from tasks.** A Task has Title + Description/Details (two body-ish
   fields); an ADR has Title + Context + Decision + Consequences (four). `TaskBodyFormat`'s
   combine/split won't directly fit - likely needs its own `AdrBodyFormat` or an extended shared
   format, following the same design principle (best-effort split on import, write-only concatenation
   is acceptable, matching how task content sync was scoped).
6. **Status mapping differs.** ADRs use `AdrStatus` (`New`/`Proposed`/`Final`/`Accepted`/`Error`/
   `Obsolete`), not `PlanningStatus`. Decide what GitHub Issue signal maps to it - open/closed state,
   labels, or (if ADRs also go on a project board) a status field like tasks use - this needs its own
   design, not a copy-paste of the task status map.
7. **Sub-issue linking implies Tasks may need to become real Issues too.** GitHub's sub-issue
   relationship links Issue-to-Issue. Today's task sync creates Draft Issues on a project board, which
   cannot be a sub-issue of a real Issue. This is a real tension to surface with the user early:
   either Tasks related to an ADR need to become real Issues (a further permission/scope increase),
   or sub-issue linking only applies to a subset of tasks, or some other resolution not yet decided.

### Suggested approach for the next session

Grill/clarify the seven open questions above with the user before writing any code - this repository's
established working pattern for this feature area is: interview to resolve open design questions →
confirm the algorithm/behavior explicitly → write the ADR documenting the decision → implement →
add tests → update `docs/adr-toc.md`/`docs/tasks-toc.md`/the user manual. Once the design is settled,
use the reference item (id `223326914`, once correctly resolved) as the first real validation target,
the same way ADR 00008/00009 were validated end-to-end against the live `github.com/users/gjkaal/projects/2`
board during development.

### Baseline to confirm before starting

Solution should build clean and all tests should pass before adding new code: from `src/`, run
`dotnet build adr.sln` and `dotnet test adr.sln --filter "FullyQualifiedName!~IntegrationTests"` -
as of this task's creation, that's 90 tests passing (0 failing) across
`Adr.Cli.Abstractions.UnitTests`, `Adr.Cli.Sync.GitHubProjects.UnitTests`, `Adr.Cli.UnitTests`, and
`Adr.Cli.Ai.AzureFoundry.UnitTests`/`.IntegrationTests`.


