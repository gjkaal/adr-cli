# 00006. Generalize sync configuration to a provider-specific settings model

2026-08-04

## Status

__New__

## Description

Per ADR 00008 (revised): replace the fixed organization/project/workItemType sync config fields with an opaque, provider-defined settings object, since Azure DevOps and GitHub Projects scope themselves too differently to share one fixed shape.

## Prerequisits

Task 00001 (`ITaskSyncProvider` abstraction and `TaskRecord` sync metadata).

## Details

- Add a `sync` section to `adr.config.json`, mirroring the existing `ai` section: a `provider` string selecting the single active connector, and a `settings` object whose shape is opaque at the `IAdrSettings` level.
- Add `TaskSyncProviderSettings` (or equivalent) alongside `AiProviderSettings`: holds `Provider` plus the raw settings payload; each `ITaskSyncProvider` implementation deserializes its own settings type from that payload independently.
- Add a `SyncSettings` property to `IAdrSettings`/`AdrSettings`, following the existing `AiSettings` wiring (parsing in `AdrSettings.cs`, default when the section is absent means no provider configured, and the registered `ITaskSyncProvider` is the no-op implementation from task 00001).
- Do not validate the inner `settings` shape at this layer - schema validation for `settings` is each provider's own responsibility, not the generic config loader's.
- This does not touch the Azure DevOps adapter (task 00002) directly, but task 00002 must be updated to read its organization/project/work-item-type configuration from this new opaque `settings` object instead of fixed top-level fields.
- Add tests covering: no `sync` section (no-op provider registered), a `sync` section with an unrecognized `provider` value, and settings round-tripping for a provider with a minimal settings shape (can use a test double provider rather than a real one).


