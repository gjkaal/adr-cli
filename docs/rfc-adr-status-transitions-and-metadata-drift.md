# RFC: status transitions, creation-time metadata drift, and `.bak` placement

**Status:** Proposed — not yet an ADR
**Raised from:** real usage of the n2adr MCP server against an unrelated repository
(`Quatro90.ImageResizer`), initialising ADRs and recording one decision end to end
**Date:** 2026-08-10

## Summary

Three issues surfaced while taking a single ADR from `adr_init` through to `Accepted` via the MCP
tools. One is an ergonomic gap that forces callers into the exact behaviour `adr_workflow_guide`
tells them to avoid; two are defects.

| # | Issue | Kind |
|---|---|---|
| 1 | No command sets an ADR's status, so status changes require hand-editing markdown | Design gap |
| 2 | `adr_new` leaves `.md` and `.json` disagreeing about status from the moment of creation | Defect |
| 3 | `adr_update_content` writes its `.bak` into the current working directory | Defect |

---

## 1. No status-transition command

### Observed

The MCP surface exposes `adr_new`, `adr_copy`, `adr_update_content`, `adr_link`/`adr_unlink`,
`adr_sync`, `adr_generate_toc`, `adr_export`/`adr_import`, and the read-only
`adr_list`/`adr_find`/`adr_get_context`. **None of them changes an ADR's status**, other than
`adr_import`, which pulls status from an external sync provider and is a no-op when none is
configured (the common local-only case).

`adr_update_content` is explicitly limited to Decision and Consequences.

### Why this is a problem

`adr_workflow_guide` item 5 states:

> NEVER edit adr.config.json, a record's .json metadata file, adr-toc.md, or tasks-toc.md directly -
> always go through the tool that owns that file

but for status **there is no tool that owns it**. Item 2 then prescribes the only available route:

> A hand edit (unlike ai:true or adr_update_content, which only ever touch Decision/Consequences) can
> also change Title/Status/Context, which do live in the .json - after a hand edit, call adr_sync

So the documented, sanctioned way to accept an ADR is: hand-edit the markdown, then remember to call
`adr_sync`, then remember to call `adr_generate_toc`. Three steps, two of them silent if forgotten,
for what is the single most common ADR lifecycle operation. An agent that follows item 5 literally
will conclude the operation is unsupported; one that follows item 2 will hand-edit a file the guide
elsewhere warns about. That contradiction is the core of this RFC.

Forgetting `adr_sync` leaves `adr_list` and `adr_find` reporting a status the document itself
contradicts — see issue 2, where this is already the out-of-the-box state.

### Proposal

Add `adr_set_status` (and the matching CLI verb):

```
adr_set_status(record: int, status: string)
```

- Validates `status` against the allowed set rather than accepting free text, so `Accepted`,
  `accepted` and `Aceptted` cannot all end up in the corpus. If the set is intended to be
  configurable, read it from `adr.config.json`.
- Writes the markdown `## Status` section **and** the `.json` metadata in one operation, so the two
  cannot diverge.
- Regenerates `adr-toc.md`, or returns an explicit reminder in its result string. The TOC contains
  status, so every status change invalidates it; making the caller remember is a footgun for the
  operation that touches status most often.
- Ideally records the transition date, so an ADR shows when it was accepted rather than only when it
  was created.

A `supersede` convenience (set the old ADR's status **and** create the `Supersedes` link in one call)
would be a natural follow-up, since `adr_new(revisionFor:)` already links the new ADR to the old one
but leaves the old one's status untouched.

If a dedicated command is unwanted, the alternative is to extend `adr_update_content` with an
optional `status` field. That is a smaller change but a worse fit for the name.

---

## 2. `adr_new` creates a record whose `.md` and `.json` disagree about status

### Observed

Immediately after:

```
adr_new(title: "Replace SixLabors.ImageSharp with SkiaSharp for image resizing",
        ai: false, context: "<supplied>")
```

- `adr_list` reported: `00002 20260810 Proposed  Replace SixLabors.ImageSharp with SkiaSharp…`
- The generated markdown's Status section contained: `__New__`
- `grep -o '"Status"[^,]*'` against `00002-….json` returned **nothing**, while the same grep against
  `00001-….json` (created by `adr_init`) returned `"Status": "Accepted"`.

No hand edits had occurred at that point; only `adr_new` and `adr_update_content` had run.

### Interpretation

Since the identical grep matches on the `adr_init`-created record, the field name and casing are
consistent in this version, which points to `adr_new` writing metadata with **no `Status` field at
all**, and `adr_list` rendering a default of `Proposed` when it is absent. I could not confirm the
internals from the outside, and the pre-sync file is gone, so treat the mechanism as inferred and the
observations above as the actual evidence.

Either way the user-visible result is the defect: **a freshly created ADR reports one status to
`adr_list` and a different one in its own document.** `adr_init`-created records do not have this
problem.

`adr_sync(record: 2)` repaired it, writing `"Status": "Accepted"` to match the markdown.

### Proposal

`adr_new` should write the status to both files at creation, from a single default. Whatever that
default is — `New`, `Proposed`, or configurable — the two files must agree without an `adr_sync`
call, and it should match the status vocabulary that `adr_set_status` (issue 1) would validate
against. If `Proposed` is genuinely the intended default, the template's `__New__` placeholder is the
thing to change.

Worth a regression test asserting that, for a newly created ADR, the markdown status and the
metadata status are equal — and that `adr_list` output matches both.

---

## 3. `adr_update_content` writes its `.bak` into the current working directory

### Observed

Calling `adr_update_content(recordId: 2, …)` created:

```
C:\git\mediachoice\Quatro90.ImageResizer\00002-replace-sixlabors.imagesharp-with-skiasharp-for-image-resizing.bak
```

That is the **repository root** — the process's working directory — not `docs/adr/` where the record
lives, and not a temp or dot-directory. Its contents were the pre-update revision of the `.md`.

### Why this is a problem

- In a git repository it appears as an untracked file at top level, mixed in with genuinely new
  source files, and is easy to `git add -A` by accident. It has no business being in version control.
- The location does not follow the record. A repo where ADRs live under a configured `adrRoot` still
  gets backup files scattered at the root, one per content update, with no cleanup.
- Nothing documents the behaviour, so a repository cannot pre-emptively `.gitignore` it. In the
  consuming repo I ended up adding `/*.bak` after the fact.

### Proposal

Pick one, in rough order of preference:

1. Write the backup **next to the record** it belongs to (`docs/adr/*.bak`) and add `*.bak` to the
   `.gitignore` guidance in the user manual.
2. Write it to a dedicated ignored location, e.g. `.adr-cli/backups/`, ideally with a retention cap.
3. Make it opt-in via `adr.config.json`, defaulting to off — the `.md` files are almost always under
   version control already, which is a strictly better backup than a sibling `.bak`.

At minimum, document the current behaviour so consumers can ignore the artefact deliberately rather
than discovering it in `git status`.

---

## Impact if not addressed

Issues 1 and 2 compound: because no tool sets status, callers hand-edit; because `adr_new` already
leaves the two files disagreeing, `adr_list` is unreliable as a status source until someone happens
to run `adr_sync`. Any tooling built on `adr_list`/`adr_find` output — dashboards, CI checks, agents
choosing which ADRs are authoritative — inherits that unreliability. Issue 3 is cosmetic by
comparison but is the one most likely to end up committed to a consumer's repository.

## Suggested sequencing

1. Issue 2 — smallest change, removes the drift at its source.
2. Issue 1 — `adr_set_status`, then update `adr_workflow_guide` so items 2 and 5 stop contradicting
   each other on status.
3. Issue 3 — independent of the other two.
