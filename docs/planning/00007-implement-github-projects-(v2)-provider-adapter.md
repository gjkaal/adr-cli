# 00007. Implement GitHub Projects (v2) provider adapter

2026-08-04

## Status

__New__

## Description

Per ADR 00008 (revised): second ITaskSyncProvider implementation, targeting GitHub Projects v2 boards (both user- and organization-owned) via GraphQL. Creates Draft Issues on export, reads/writes a configurable single-select status field, and authenticates via a GitHub PAT.

## Prerequisits

Task 00001 (provider abstraction and sync metadata) and task 00006 (provider-specific settings model). Should follow the same shape as task 00002's Azure DevOps adapter where the concerns overlap.

## Details

- Create a new `Adr.Cli.Sync.GitHubProjects` implementation project (plus a matching `.UnitTests` project), isolating the GitHub GraphQL client dependency from the core CLI, mirroring how `Adr.Cli.Ai.AzureFoundry` isolates its own SDK.
- Define the GitHub Projects settings shape deserialized from task 00006's opaque `settings` object: `ownerType` (`User` or `Organization`, required, never auto-detected), `owner`, `projectNumber`, and `statusFieldName` (defaults to `"Status"`).
- Resolve the project board's GraphQL node id using `user(login:...)` or `organization(login:...)` per the configured `ownerType`, then `projectV2(number: projectNumber)`.
- Authenticate using a personal access token read from the `ADR_CLI_SYNC_GITHUB_PAT` environment variable. No interactive-login or `DefaultAzureCredential`-style fallback for this provider.
- Export behavior:
  - When the task has no external identifier yet, create a Draft Issue on the resolved project board.
  - Map `Title` to the draft issue's `title`. Concatenate `Description` and `Details` (with a separator) into the draft issue's single `body` field - this mapping is write-only, since import never reads `Description`/`Details` back.
  - Push the local `PlanningStatus` to the board's status field (named by `statusFieldName`) using the export's local-to-external status map from task 00004/00008's status-map work, by setting that single-select field's option on the project item.
  - When an external identifier already exists, update the existing draft issue's title/body and status field option instead of creating a new item.
- Import behavior: read the current option value of the `statusFieldName` single-select field for the linked project item, and map it to a local `PlanningStatus` via the import status map. An option with no configured mapping leaves the task's sync state unmapped (per task 00001's `SyncState`), not guessed.
- Only single-select field values are supported for `statusFieldName`; fail clearly (not silently) if the configured field is a different field type or does not exist on the board.
- Handle GitHub API errors (auth failure, rate limiting, missing project/field, wrong owner type) with actionable messages; leave retry/backoff behavior as an implementation detail per ADR 00008.
- Ensure the PAT is never written to task files, sync metadata, command output, MCP responses, or logs.
- Add automated tests covering: user-owned vs organization-owned project resolution, draft issue create vs update, title/body mapping, status field read/write against both maps, unmapped-status handling, and PAT redaction. Add an integration test project mirroring `Adr.Cli.Ai.AzureFoundry.IntegrationTests`'s pattern if real-API verification is wanted, skipped by default like that one.


