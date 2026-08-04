# 00003. Linking ADR's is a wa to tie ADR's together

2025-10-28

## Status

__New__

Extends [00002.W need unit tests to enhance code quality](.\00002-w-need-unit-tests-to-enhance-code-quality)

## Context

As the number of ADRs in a repository grows, individual decisions increasingly relate to one
another - one ADR may extend, amend, clarify, or supersede an earlier one. Without a way to record
these relationships, each ADR reads as an isolated document, and a reader has no way to discover
that a later decision changed the assumptions or scope of an earlier one short of reading the whole
repository by hand.

## Decision

Add `link`/`rlink` commands (and their MCP/CLI equivalents) that record a directed relationship from
a source ADR to a target ADR, identified by their numeric record ids. Each `AdrRecord` gains a
`References` dictionary (target id -> a free-text remark, e.g. "Extends", "Supersedes", "Clarifies").
`link -s <source> -t <target> -r <remark>` adds an entry to the source's `References`, and also
inserts a human-readable line under the source ADR's `## Status` section in its markdown (e.g.
"Extends [00002.W need unit tests to enhance code quality](./00002-...)"), so the relationship is
visible both to tooling (via the JSON metadata) and to anyone reading the markdown directly. Linking
the same source/target pair again with an additional remark accumulates it (remarks are joined with
`;`) rather than overwriting or duplicating the entry. `rlink -s <source> -t <target>` removes the
reference - and its corresponding markdown line(s) - entirely, regardless of which remark(s) it
carried.

A link is one-directional: linking ADR 3 to ADR 1 records the relationship only on ADR 3's side. If
the relationship should be discoverable from both ends (e.g. "3 extends 1" and "1 is extended by
3"), the reverse link must be added explicitly as its own `link` call - there is no automatic
reciprocal linking, keeping the command's mental model simple: one command, one direction, one
remark.

## Consequences

ADRs can express relationships to each other, and the JSON metadata (`References`) and markdown
content stay consistent, since `link`/`rlink` update both files in the same operation. Linking works
even when the target ADR's JSON metadata is missing, reconstructing it from markdown first - so
linking tolerates a `.json` file that has drifted out of sync with its `.md` file.

Because linking is one-directional, a bidirectional relationship needs two explicit `link` calls;
nothing enforces that the reverse link is ever added, so a repository can end up with a link that
only one side "knows about". Nothing validates that a `remark` comes from a controlled vocabulary -
"Extends", "Supersedes", etc. are conventions, not enforced values - so a remark's meaning depends on
consistent usage by whoever is linking.

`References` is scoped to ADR-to-ADR links only; it does not cover linking an ADR to a Task. ADR
00010 later added a separate `RelatedTasks` field for that, rather than overloading `References`,
since Tasks and ADRs number their records independently and a shared id-keyed dictionary could not
tell the two apart.

