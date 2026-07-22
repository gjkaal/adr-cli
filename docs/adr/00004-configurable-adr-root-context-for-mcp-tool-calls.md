# 00004. Configurable ADR root context for MCP tool calls

2026-07-21

## Status

__Accepted__

## Context

`AdrSettings` (`src/Adr.Cli/AdrSettings.cs`) resolves which `adr.config.json` is "active" exactly
once, at construction time: `currentPath` is seeded from `Directory.GetCurrentDirectory()`
(line 32), and `GetConfigFileInfo()` (lines 262-292) then walks **upward** through parent
directories only, stopping at the first `adr.config.json` it finds, falling back to a
machine-wide config in `CommonApplicationData` if nothing is found before hitting the drive root.
For a long-lived MCP server process, this resolution happens once and is fixed for the entire
session — there is no per-call way to point a tool at a different config, and the search can never
reach *into* a subdirectory, only up toward ancestors.

This becomes a real problem in a multi-repo workspace where more than one `adr.config.json`
exists at different nesting depths — which is a normal, supported setup (every `adr_init`
invocation creates one), not a misconfiguration. Concrete example hit while using the `n2adr` MCP
server against `C:\git\mediachoice`: that workspace root has its own `adr.config.json`
(`ProjectName: "mediachoice"`, auth/backend-related ADRs), and a nested repository,
`pipeline-templates`, has a **separate** `adr.config.json` (`ProjectName: "pipeline-templates"`,
CI/CD-related ADRs) one level down. Calling `adr_list` while working on `pipeline-templates`
silently returned the *mediachoice root's* ADRs (SAML/auth topics, completely unrelated) — the
upward search resolved to the ancestor config and had no way to prefer or even see the nested one.
There was no error, warning, or indication that a different project's ADR trail was in scope than
intended. The only reason this surfaced was a manual sanity check before calling `adr_new` — had
that call gone ahead, a new ADR for `pipeline-templates` would have been silently written into the
`mediachoice` root's `docs/adr/` instead, misfiled under the wrong project with the wrong
`RecordId` sequence.

A partial precedent for solving this already exists: the `adr_init` MCP tool
(`AdrMcpServer.cs`, lines 37-50) accepts optional `adrRoot`/`tmpRoot`/`prjRoot` parameters to
target a specific folder structure — but this is wired up only for one-time repository
initialization. None of the read/write tools that operate against an *already-initialized* repo
(`adr_new`, `adr_list`, `adr_generate_toc`, `adr_link`, `task_*`, etc.) accept anything equivalent;
they all go through `AdrSettings`'s fixed, session-wide, upward-only resolution.

## Decision

Combine options 2 and 3 from the original candidate list; drop option 4; do not pursue option 1.

- **Option 4 is dead, not merely deprioritized.** The second reproduction (below) shows the
  resolved root was a *third*, wholly unrelated project — not an ancestor of the caller's actual
  working directory at all. That disproves the premise option 4 depended on (that the server's
  cwd bears *some* relation to the caller's cwd worth searching from). There is nothing to make
  "smarter" here: the server process's cwd has no reliable relationship to the calling session's
  repository, so only the caller — which does know its own working directory — can supply the
  correct root.
- **Option 2 (`adr_set_context`), not option 1 (per-call parameter).** Both require the caller to
  supply the root, but an LLM-driven MCP client is more likely to forget a parameter on the
  *n*-th tool call of a session than to make one explicit "switch context" call at the start of
  it. A session-scoped pin also matches how these sessions are actually used in practice: work is
  concentrated on one repository at a time.
- **Option 3 ships alongside it, unconditionally.** `adr_set_context` only helps sessions that
  remember to call it. Every tool response — success or failure — is stamped with the
  `adr.config.json` path and `ProjectName` actually in effect, so a caller that forgets to pin, or
  inherits a stale pin, has a visible, checkable fact instead of a silent misfile.

Implementation shape:

- `IAdrSettings` gains `AdrContextInfo CurrentContext` (read-only snapshot: `ProjectName`,
  resolved `adr.config.json` path, `CurrentPath`, `DocFolder`, `TasksFolder`, `TemplateFolder`) and
  `AdrContextInfo TrySetContext(string workingDirectory)`.
- `TrySetContext` searches upward from `workingDirectory` for an existing `adr.config.json` only —
  **no auto-creation, and no fallback to the machine-wide `CommonApplicationData` config.** If none
  is found, it returns a failure and leaves all existing settings untouched. This was a deliberate
  constraint: a context-switch tool must never have an implicit "just initialize one here" escape
  hatch, or an agent calling it speculatively across directories would scaffold ADR repositories
  wherever it happened to look. `adr_init` remains the only way to create a new repository.
- It is purely in-memory — it never calls `settings.Write()`. This deliberately avoids the
  existing side effect in `AdrInit.InitializeAsync` (used by `adr_init`'s `adrRoot`/`tmpRoot`/
  `prjRoot` params), where passing a path *permanently* overwrites the real `adr.config.json` on
  disk. That existing behavior is a separate, pre-existing wart — worth fixing on its own, but out
  of scope here — and confirms those params were never real precedent for this problem: they
  rename a subfolder under the already-resolved root, they don't select a different repository.
- A new MCP tool, `adr_set_context(workingDirectory)`, is the only caller of `TrySetContext`.
- The context stamp is appended in exactly one place, `AdrMcpServer.CallToolAsync`, after each
  tool's formatted text result is produced (success or exception path) — not duplicated across
  each of the 16 individual tool handlers, and not added as a field on `McpCore.Response` (whose
  parameterless `Ok()`/`Fail()` return cached, shared static instances — mutating a per-call field
  on those would leak across unrelated calls).

## Consequences

- A caller in a multi-repo workspace must explicitly call `adr_set_context` once per session
  before other tools if it wants to point at something other than whatever root the server
  process resolved at startup. This is a manual step, not automatic detection — accepted as the
  necessary cost of there being no reliable signal for "caller's real cwd" available to the server.
- Every tool response now carries a visible `[adr-cli context: ...]` stamp. This is a small,
  permanent verbosity cost on every call, in exchange for making today's silent misrouting failure
  mode impossible to miss.
- The pre-existing `adrRoot`/`tmpRoot`/`prjRoot` params on `adr_init`, and their unconditional
  `settings.Write()` side effect, are unchanged by this decision and remain a separate known wart.
- Concurrency is not a concern for `TrySetContext` mutating the shared `IAdrSettings` singleton:
  the MCP server processes stdin requests strictly sequentially (`Program.cs` awaits each
  request fully before reading the next line), so there is no in-flight call from a different
  "session" to race with.

## Update, 2026-07-22 — second reproduction, worse than originally documented

Hit again while trying to create/supersede an ADR in `pipeline-templates`
(`00008-floating-latest-tag-on-development-calver-tag-on-main.md`, superseding `00004` there).
This time `adr_list` didn't resolve to the `mediachoice` root's ADRs (the ancestor-config case this
RFC originally described) — it returned a *third*, completely unrelated project's ADRs entirely
(SAML/OpenIdConnect/tenant-seat-budget topics, no relation to `pipeline-templates` or
`mediachoice`). No parameter on `adr_new`/`adr_list`/`adr_link`/`adr_sync`/`adr_generate_toc`
allows overriding this.

This means the resolved root isn't reliably "the nearest ancestor `adr.config.json` from the
caller's cwd" (option 4's framing) — it's whatever `Directory.GetCurrentDirectory()` was for the
MCP *server process* at construction time, which can be a directory from a wholly unrelated prior
session/repo, with zero relation to what the calling session is currently working on. This
strengthens the case for option 3 (surface which config was used in every response) as a
minimum-bar fix regardless of which other option is chosen: without it, there is no way to even
detect this failure mode short of manually checking output content against what you expected, as
happened here. The ADR in `pipeline-templates` was created by writing the file directly instead,
bypassing the MCP tool entirely for this session.
