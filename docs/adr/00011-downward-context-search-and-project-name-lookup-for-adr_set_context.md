# 00011. Downward context search and project-name lookup for adr_set_context

2026-08-05

## Status

__Accepted__

Extends [00004.Configurable ADR root context for MCP tool calls](.\00004-configurable-adr-root-context-for-mcp-tool-calls)

## Context

ADR 00004 fixed `AdrSettings`'s config resolution so a caller could explicitly pin an MCP session to a specific repo via `adr_set_context(workingDirectory)` - but that fix only searched **upward** from the given directory. It could resolve an exact repo (or any directory inside one), but had no way to discover a repository nested *under* a given directory.

This became a real problem again on 2026-08-05, working against `C:\git\mediachoice\MediaChoice.Net.Core.Utilities.MassTransit`. The MCP server process's own working directory was `C:\git\mediachoice` (the multi-repo workspace root), which - separately - had accumulated its own stray `adr.config.json` and a duplicate `docs/adr` tree (containing the exact same ADR titles as the MassTransit subrepo, evidence of an earlier session having written ADRs to the wrong root before `adr_set_context` existed). `adr_get_context` correctly reported this stray parent config, and the only fix available was `adr_set_context` pointed at the exact subrepo folder - there was no way to ask "what ADR repositories exist under this workspace root?" without knowing the subfolder name in advance.

Investigating the corrupted stray files also surfaced a second, unrelated bug: `AdrSettings.Write()` used `FileMode.OpenOrCreate` (which does not truncate) and only serialized `Path`/`Templates`, so any repeat `adr_init` call against an existing, richer config silently dropped `ProjectName`/`Tasks`/`Ai`/`Sync` and could leave corrupted trailing bytes from the previous, longer file - exactly matching a corrupted `adr.config.json.old` found on disk. That bug is fixed alongside this decision (`AdrSettings.Write()` now truncates and round-trips all fields; `AdrInit` no longer calls `Write()` on a redundant already-initialized call) but is a separate defect, not the subject of this ADR.

## Decision

Extend `TrySetContext(workingDirectoryOrProjectName)` rather than adding a separate "discover" tool, so
`adr_set_context` keeps being the single entry point a caller needs to remember:

- **Directory input**: search upward first, exactly as ADR 00004 already does - an exact match (the
  given directory is inside a known repo) resolves immediately and unambiguously, unchanged from
  today. Only if nothing is found upward does the search fall back to searching **downward** into
  the directory's subfolders, bounded to 4 levels deep and skipping build/dependency/hidden folders
  (`bin`, `obj`, `node_modules`, `packages`, `dist`, `build`, and anything starting with `.`). The
  bound exists purely to keep scan cost predictable in a large workspace; it is not a correctness
  requirement - a repository nested deeper than 4 levels under the given root simply won't be found,
  which is an acceptable, easily-diagnosed limitation (searching upward from inside that repo still
  works).
- **Project-name input**: when the given value isn't an existing directory at all, match it
  case-insensitively against `ProjectName` in whatever set of `adr.config.json` files the *most
  recent* downward search found, re-scanning from the current root only if no such set exists yet.
  This makes the natural two-step flow work without re-scanning the disk on every call: point
  `adr_set_context` at a workspace root, get back a list of candidates, then call it again with one
  of the names from that list.
- **Ambiguous results are surfaced, never guessed.** Whether ambiguity comes from multiple nested
  repos under a directory, or multiple projects sharing a name, `TrySetContext` returns
  `Success = false` with a new `Candidates` list (`ProjectName` + `FolderPath` per entry) instead of
  picking one. `AdrContext.SetContextAsync` renders this as a numbered-looking list in the tool
  response so an LLM-driven caller can just relay it to the user or retry with a specific entry -
  this mirrors ADR 00004's own principle of making a wrong-context situation visible rather than
  silent.
- **A config found during downward search is a terminal node**, not a recursion root - if a folder
  has its own `adr.config.json`, the search does not continue into its subfolders looking for more.
  A nested config inside an already-discovered repo is treated as that repo's own business, not a
  second candidate.
- Construction-time resolution (`AdrSettings`'s constructor / `Read()`) is deliberately **not**
  changed - it still only searches upward and falls back to the machine-wide default. There is no
  way to present a disambiguation choice at that point (nothing has asked a question yet, and
  construction cannot fail), so downward search and name lookup are only exposed via the explicit,
  interactive `adr_set_context` path.

## Consequences

- Pointing `adr_set_context` at a multi-repo workspace root now works as a discovery step: with zero
  prior knowledge of subfolder names, a caller gets back every ADR project nested under that root
  (verified against the real `C:\git` tree during implementation: 29 real repositories found
  correctly, correctly skipping `bin`/`obj`/`node_modules`/`.git`/etc., in about a second).
- The `workingDirectory` parameter on the `adr_set_context` MCP tool now silently accepts two
  different kinds of value (a path or a project name) distinguished only by whether it happens to
  exist as a directory - a project literally named the same as an existing folder path would be
  interpreted as the folder. This is an accepted ambiguity: project names and paths overlapping this
  way is vanishingly unlikely in practice, and the alternative (a second, explicit parameter) adds
  friction to the common case for a theoretical edge case.
- A repository nested more than 4 folder levels below the directory passed to `adr_set_context` will
  not be found by the downward search and will report "no candidates found" rather than listing it -
  the workaround is to call `adr_set_context` with a directory closer to (or inside) that repository,
  where the existing upward search still applies without any depth limit.
- `lastDiscoveredCandidates` is process-lifetime, in-memory state on the `AdrSettings` singleton -
  consistent with ADR 00004's existing concurrency argument (the MCP server processes stdin
  requests strictly sequentially, so there is no in-flight call from a different "session" to race
  with). A name-based `adr_set_context` call made without ever having run a directory-based one first
  still works: it triggers a fresh downward search from the current root rather than failing.
- The underlying lesson from the incident in the Context section stands regardless of this fix:
  always call `adr_get_context` first and `adr_set_context` explicitly in a multi-repo workspace,
  rather than trusting whatever context the MCP server resolved by default.

