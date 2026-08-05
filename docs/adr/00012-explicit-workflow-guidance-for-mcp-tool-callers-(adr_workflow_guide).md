# 00012. Explicit workflow guidance for MCP tool callers (adr_workflow_guide)

2026-08-05

## Status

__Accepted__

Related to [00011.Downward context search and project-name lookup for adr_set_context](.\00011-downward-context-search-and-project-name-lookup-for-adr_set_context)

## Context

While creating ADR 00011, the assistant used direct file edits (Read/Edit) to author an ADR's Decision/Consequences content and only reconciled that back through `adr_sync`/`adr_link`/`adr_generate_toc` after being explicitly told to. This wasn't a one-off lapse: nothing in the MCP surface states the intended workflow up front. Each tool (`adr_new`, `adr_sync`, `adr_link`, `adr_set_context`, etc.) documents its own contract reasonably well, but there is no equivalent to an onboarding call (the kind of thing a caller invokes once at the start of a session to learn "how work gets done here") - a caller has to reconstruct the overall workflow from individual tool descriptions, or from reading the tool's own source, neither of which is reliable.

Concretely, two facts were not discoverable from the tool surface alone:
- Decision/Consequences (and a task's Details) are markdown-only fields with no corresponding "set" tool - the only ways to populate them are AI drafting (`ai:true`) or a direct hand-edit of the .md file, followed by `adr_sync` to reconcile the .json metadata. `adr_sync`'s own description hints at this ("use after manually editing... outside of adr-cli") but nothing states it as the *expected* path for tool-driven ADR authoring too, nor connects it to `adr_new` leaving those fields blank.
- Several files (`adr.config.json`, each record's .json metadata, the generated TOC files) must only ever be modified through their owning tool, never edited directly - this is implied by the existence of `adr_sync`/`adr_generate_toc` but never stated as a rule.

## Decision

Do both things considered, rather than picking one:

- **Add a new tool, `adr_workflow_guide`** (no arguments, changes nothing), modeled on the pattern of
  an onboarding call a caller invokes once at the start of a session - not automatically triggered
  (MCP has no server-initiated push to a caller, and ADR 00004 already established a preference for
  visible, explicit signals over implicit magic), but named and described so that "call this first"
  is obvious from the tool listing alone, and listed first in the tool array. It returns a single
  static block of text covering: multi-repo context safety; that Decision/Consequences/Details are
  markdown-only with no setter tool and must be hand-edited or AI-drafted then reconciled via
  `adr_sync`; that relationships go through `adr_link`/`adr_unlink`/`adr_link_task`, never a manual
  markdown edit; that `adr_generate_toc`/`task_generate_toc` must be run after changes; and which
  files (`adr.config.json`, per-record `.json` metadata, both TOC files) must never be edited
  directly. This text deliberately contains **no ADR number references** - it ships inside the tool
  itself and runs against whatever repository has adr-cli installed, which has no guaranteed
  relationship to this project's own `docs/adr` folder; a caller in a different repository would
  find a citation to "ADR 00004" meaningless or, worse, misleading. It does, however, link to the
  adr-cli user manual (`https://github.com/gjkaal/adr-cli/blob/trunk/src/User%20manual.md`) for the
  full `adr.config.json` schema and command-by-command usage - unlike an ADR number, that manual
  documents the tool itself and is equally valid regardless of which repository adr-cli is running
  against.
- **Also strengthen the one existing tool description that was the direct proximate cause**:
  `adr_new`'s description now states plainly that there is no tool to set Decision/Consequences
  directly, and names the hand-edit-then-`adr_sync` path as the expected route - rather than leaving
  that fact only inferable from `adr_sync`'s own description. Per-tool descriptions remain the
  source of truth for that tool's own contract; the workflow guide is additive context, not a
  replacement for accurate individual descriptions.

Considered and rejected: relying on `adr_sync`'s existing description alone (already tried, not
sufficient - it explains its own contract, not that this is the intended authoring workflow for
every ADR, including ones created through `adr_new`); and stamping the workflow reminder onto every
tool response (rejected as unnecessary verbosity on every single call for a fact a caller only needs
once per session - contrast with ADR 00004's context stamp, which carries state that can change
between calls and so must appear on every one).

## Consequences

- A caller (human or LLM-driven) now has one obvious place to learn the intended workflow instead of
  reconstructing it from source or from scattered tool descriptions - but only if it actually calls
  `adr_workflow_guide`. Nothing forces this; a caller that skips it is in exactly the same position as
  before this decision.
- The guide text is static and lives in code (`AdrMcpServer.WorkflowGuideText`), so it will drift out
  of date if the underlying tools change behavior without a corresponding update - same maintenance
  risk as any other tool description, not a new one introduced here.
- `adr_new`'s description is now noticeably longer. Accepted as worthwhile: the fact it now states
  (no Decision/Consequences setter; hand-edit then `adr_sync`) is exactly the one a caller needs at
  the moment it calls that tool, not only in the separate guide.
- The workflow guide intentionally omits ADR number citations, unlike this project's other MCP tool
  descriptions (`adr_export`/`adr_import`/`task_export`/`task_import`/`adr_link_task`, which do cite
  specific ADR numbers from this repository's own history). Those pre-existing citations are outside
  this decision's scope and were left unchanged - fixing them consistently is a follow-up, not
  required for `adr_workflow_guide` to be useful.

## Update, 2026-08-05 - a direct setter was added after all

While reviewing why AI-drafted Decision/Consequences sometimes came back empty (a separate,
unrelated bug in `IsWellFormed`'s Pro's/Con's check), a convenience tool/command was requested:
`adr_update_content` (MCP) / `update-content` (CLI) now replace Decision and/or Consequences on an
existing ADR directly, in place - no editor, no hand-editing the `.md` file. This doesn't contradict
the "no setter tool" framing above so much as complete it: hand-edit and `adr_update_content` are
now two ways to reach the same markdown-only fields, and `adr_workflow_guide`'s text and `adr_new`'s
description have both been updated to mention it. The core Decision above (workflow guide as an
onboarding tool, no ADR-number citations in guide text) is unaffected.

