# 00005. Add per-task success and failure reporting for batch export and import

2026-07-22

## Status

__New__

## Description

Per ADR 00008: when exporting or importing multiple tasks in one operation, report each task's success or failure individually so one failure does not obscure or block the results for the rest of the batch.

## Prerequisits

No prerequisits.

## Details

- Process every requested task independently, continuing the batch after task-specific validation, provider, mapping, or persistence failures.
- Capture a result for each task that includes:
  - The local task identifier.
  - Whether the operation succeeded or failed.
  - A concise success summary or actionable error message.
  - Relevant external identifiers when available, without exposing credentials or other secrets.
- Return results in a stable format suitable for both human-readable CLI output and structured MCP responses.
- Include aggregate counts for succeeded, failed, and total tasks.
- Ensure CLI exit behavior indicates when any task failed while still printing the complete batch report.
- Preserve explicit unmapped-status outcomes from status import, including unknown or ambiguous external status mappings, rather than reporting them as successful imports.
- Preserve a distinct "skipped" outcome (not success, not failure) for tasks whose sync link belongs to a provider other than the currently active one (see task 00004), so a stale link from a previous connector doesn't read as an error.
- Add tests covering all-success, partial-failure, all-failure, and mixed-with-skipped batches, including verification that later tasks are processed after an earlier failure.
- Coordinate the result model and output behavior with tasks 00003 and 00004; provider-specific behavior remains within the adapters implemented by tasks 00002 and 00007.


