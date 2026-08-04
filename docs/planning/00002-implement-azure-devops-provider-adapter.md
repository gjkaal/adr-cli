# 00002. Implement Azure DevOps provider adapter

2026-07-22

## Status

__New__

## Description

Per ADR 00008 (revised): first ITaskSyncProvider implementation. Handles Azure DevOps authentication, work-item creation/update, and field translation between TaskRecord and Azure DevOps work items. Credentials come from a PAT environment variable only, never written to task files.

## Prerequisits

Task 00001 (provider abstraction and sync metadata) and task 00006 (provider-specific settings model).

## Details

- Build on the provider abstraction and sync metadata introduced by task 00001.
- Create a new `Adr.Cli.Sync.AzureDevOps` implementation project (plus a matching `.UnitTests` project), isolating Azure DevOps SDK dependencies from the core CLI.
- Read organization, project, and work-item-type configuration from task 00006's opaque provider `settings` object, not from fixed top-level config fields.
- Authenticate using a personal access token read from the `ADR_CLI_SYNC_AZUREDEVOPS_PAT` environment variable only. No `DefaultAzureCredential`/interactive-login fallback for this provider.
- Map `Title`/`Description`/`Details` to distinct Azure DevOps work-item fields.
- Export also pushes the local `PlanningStatus` to an external work-item state, using an explicit local-to-external status map (independently configured, not derived from the import map - see task 00004).
- Create a new work item when the task has no Azure DevOps external work-item ID.
- Update the existing work item when Azure DevOps sync metadata is present, avoiding duplicate creation within a single clone (duplicate creation across clones that both lack the sync metadata is a documented, unsolved limitation of ADR 00008, not something this task needs to detect or prevent).
- Return or persist the external scope and work-item ID through the provider-agnostic synchronization flow.
- On import, read the external work-item state and leave sync state unmapped (per task 00001) when the external status has no entry in the external-to-local status map, rather than guessing.
- Handle unsupported field values, missing configuration, authentication failures, authorization failures, and Azure DevOps API errors with actionable messages.
- Ensure secrets are excluded from task serialization and are redacted from diagnostic output.
- Add automated tests covering field translation, create-versus-update behavior, configuration validation, status push (export) and status pull (import) mapping, unmapped-status handling, API error handling, and credential redaction.


