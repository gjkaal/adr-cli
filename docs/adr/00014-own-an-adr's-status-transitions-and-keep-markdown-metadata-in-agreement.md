# 00014. Own an ADR's status transitions and keep markdown/metadata in agreement

2026-09-11

## Status

__Accepted__

## Context

See docs/rfc-adr-status-transitions-and-metadata-drift.md for the full analysis. Summary: (1) no MCP/CLI tool owned an ADR's Status, so adr_workflow_guide's own item 2 prescribed hand-editing the markdown plus adr_sync as the only route to accepting an ADR - the single most common ADR lifecycle operation - while item 5 separately forbade hand-editing metadata, a direct contradiction; (2) adr_new left a freshly created ADR's .md and .json disagreeing about status from the moment of creation, traced to AdrStatus.New being the enum's zero value colliding with JsonIgnoreCondition.WhenWritingDefault, which silently dropped the Status field from the .json; (3) adr_update_content wrote its .bak backup into the process's current working directory (the repository root under the n2adr MCP server) instead of next to the record, because FileInfo.CopyTo resolves a directory-less destination against the cwd, not the source file's folder.

## Decision

Add adr_update_status / update-status: writes the markdown '## Status' section and the .json metadata in one operation so they cannot disagree, appends a status-log entry (new AdrStatusUpdate + AdrRecord.Logs, mirroring TaskRecord's existing StatusUpdate/Logs), and regenerates adr-toc.md automatically since every status change invalidates it. adr_workflow_guide items 2 and 5 now name this tool as the sole owner of Status instead of prescribing a hand-edit + adr_sync round trip.

Reorder AdrStatus so None=0 is a true sentinel (mirroring PlanningStatus's existing None=0/New=1 layout) and New/Proposed/Final/Accepted/Error/Obsolete shift to 1-6, so a freshly created ADR's Status (New) no longer collides with JsonIgnoreCondition.WhenWritingDefault's CLR-default check and silently vanish from the .json. Align AdrRecord.Status's C# default to New to match every real creation path.

Fix task_update to write its task's markdown Status section as well as the .json (previously .json-only, on every call, with no task_sync equivalent to repair the drift), and regenerate tasks-toc.md the same way.

Fix DocumentBasedRepository.UpdateFileContentAsync to build the .bak backup path from the source file's own directory instead of a bare filename, since FileInfo.CopyTo(destFileName, overwrite) resolves a directory-less destination against the process's current working directory, not the source file's folder. Also fixed the riding-along cleanup bug where the delete-after-write check was built through a path that never matched the real backup location, so backups were never actually deleted.

## Consequences

*Pro's:*
- adr_list/adr_find are now reliable as a status source immediately after adr_new, with no adr_sync round trip required.
- Accepting/rejecting/obsoleting an ADR is a single tool call, matching the tool's own workflow guide instead of contradicting it.
- Every ADR and task now carries a full status history (timestamp + justification), not just its current state.
- .bak files no longer leak into a consuming repository's working directory root.

*Con's:*
- AdrStatus's underlying numeric values changed (None=0, New=1..Obsolete=6). Harmless for any repository using the JsonStringEnumConverter-based .json format (the only supported format), but would break a hypothetical integration relying on the old raw numeric values.
- adr_update_status/task_update accept any status-to-status transition with no lifecycle validation (e.g. Accepted -> New is allowed) - deliberately, to match the existing task_update precedent and avoid inventing lifecycle policy beyond what this RFC asked for, but a future RFC may want to revisit this.
- A supersede convenience (set the old ADR's status and create the Supersedes link in one call) remains unimplemented, left as a natural follow-up noted in the original RFC.


