# 00002. Implement Azure DevOps provider adapter

2026-07-22

## Status

__New__

## Description

Per ADR 00008: first ITaskSyncProvider implementation. Handles Azure DevOps authentication, work-item creation/update, and field translation between TaskRecord and Azure DevOps work items. Credentials come from secure configuration/environment, never written to task files.

## Prerequisits

No prerequisits.

## Details

- Build on the provider abstraction and sync metadata introduced by task 00001.
- Implement Azure DevOps authentication and validate required organization, project, and credential configuration.
- Map relevant `TaskRecord` fields—such as title, description, status, details, and related metadata—to Azure DevOps work-item fields.
- Create a new work item when the task has no Azure DevOps external work-item ID.
- Update the existing work item when Azure DevOps sync metadata is present, avoiding duplicate creation.
- Return or persist the external project/scope and work-item ID through the provider-agnostic synchronization flow.
- Handle unsupported field values, missing configuration, authentication failures, authorization failures, and Azure DevOps API errors with actionable messages.
- Ensure secrets are excluded from task serialization and are redacted from diagnostic output.
- Add automated tests covering field translation, create-versus-update behavior, configuration validation, API error handling, and credential redaction.


