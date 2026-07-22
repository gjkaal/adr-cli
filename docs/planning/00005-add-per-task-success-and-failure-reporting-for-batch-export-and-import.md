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
- Preserve explicit unsynchronized outcomes from status import, including unknown or ambiguous external status mappings, rather than reporting them as successful imports.
- Add tests covering all-success, partial-failure, and all-failure batches, including verification that later tasks are processed after an earlier failure.
- Coordinate the result model and output behavior with tasks 00003 and 00004; provider-specific behavior remains within the adapter implemented by task 00002.


