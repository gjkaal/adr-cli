# 00008. Export tasks and import task status updates to and from external applications (e.g. Azure DevOps)

2026-07-22

## Status

__New__

## Context

Tasks created and maintained in this repository may need to be tracked in external work-management applications such as Azure DevOps. Manually recreating tasks externally and copying status changes back into the repository is time-consuming, error-prone, and can cause the two representations to diverge.

The repository should remain the authoritative source for task content, while external applications may be used for assignment, planning, and workflow tracking. Integration therefore requires a stable association between each local task and its external work item, configurable mappings between local and external statuses, and protection against duplicate exports or unintended overwrites.

Different external applications expose different authentication models, work-item types, fields, and APIs. The integration must not couple the task model directly to Azure DevOps or require provider-specific behavior throughout the codebase. In accordance with ADR 00005, equivalent export and import capabilities must also be available through both CLI commands and MCP tools.

## Decision

Implement task synchronization through a provider-based integration layer, with Azure DevOps as the first supported provider.

Local task files remain the authoritative source for task title, description, and details. Exporting a task creates or updates its corresponding external work item. Importing is limited to supported workflow data, initially the external status, which is mapped to a local task status through provider configuration.

Each exported task stores or references synchronization metadata containing at least the provider, external project or scope, and external work-item identifier. Export operations use this metadata to update an existing work item rather than create a duplicate. Tasks without an external identifier are created externally and linked after a successful export.

Provider adapters are responsible for authentication, API communication, field translation, and provider-specific error handling. Shared application services coordinate synchronization so that CLI commands and MCP tools expose equivalent behavior without duplicating integration logic. Credentials and access tokens are supplied through secure configuration or environment mechanisms and are not written to task files or synchronization metadata.

Import and export are explicit user-initiated operations. Status mappings are configurable, and an unknown or ambiguous external status causes the task to be reported as unsynchronized rather than silently assigning an incorrect local status. Batch operations report individual successes and failures so that one failed task does not obscure the result for the remaining tasks.

## Consequences

Tasks can be transferred to external planning systems and have their workflow status reflected locally without requiring manual duplication. Stable external identifiers make repeated exports idempotent and provide traceability between local tasks and external work items.

The provider abstraction allows additional applications to be supported without changing the core task model or synchronization workflow. Sharing the synchronization services between the CLI and MCP interfaces preserves the capability equivalence established by ADR 00005.

The repository remains authoritative for task content, which avoids general-purpose bidirectional merge behavior and reduces the risk of external edits unexpectedly overwriting local information. However, changes made externally to titles, descriptions, or other unsupported fields are not imported and may be overwritten by a later export.

Configuration is required for provider endpoints, projects, work-item types, field mappings, status mappings, and credentials. Provider API changes, rate limits, permissions, and temporary failures introduce operational complexity and require clear diagnostics and retry-safe operations.

Synchronization metadata becomes part of the task lifecycle. Deleted, moved, duplicated, or externally removed tasks may require reconciliation, and invalid mappings must be surfaced for manual resolution. Automated or continuous synchronization is not included in this decision and can be considered separately if explicit synchronization proves insufficient.
