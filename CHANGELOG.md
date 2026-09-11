# Changelog

## Unreleased

### Fixes

- Fixed non-ASCII characters (e.g. em dashes) getting corrupted when written through MCP tool calls (`adr_new`, `adr_update_content`, `task_new`) on Windows. `Console.InputEncoding`/`OutputEncoding` are now forced to UTF-8 at startup instead of defaulting to the process's OEM codepage for redirected stdio, which was silently mis-decoding any non-ASCII character read from stdin.
- Normalized typographic dashes (em dash, en dash, horizontal bar) to a plain ASCII hyphen in ADR/task Title, Context, Decision, Consequences, Description, and Details content, whether typed manually or drafted by AI - see `TextSanitizerExtensions.NormalizeDashes`.

## 1.0.0.10 — 2026-07-22

### AI-assisted drafting

- Extended AI-assisted drafting to also fill in an ADR's Context, not just Decision and Consequences; the model now returns all three sections in one grounded reply, using the ADR's own template as a style example.
- Added AI-assisted drafting for tasks (`ITaskProposalGenerator` / `AzureFoundryTaskProposalGenerator`), drafting Description and Details independently from ADR drafting - see ADR 00007. `task-new` / `task_new` gained an `--ai` / `ai` option to match.
- Clarified MCP tool and CLI descriptions for `adr_new`/`new` and `task_new`/`task-new` to state the ADR-vs-Task distinction explicitly.
- Documented the Azure DevOps task export/import integration decision (ADR 00008) and drafted an initial task backlog for it.

### Fixes

- Fixed AI proposal generation failing outright when the on-disk template couldn't be read - template lookup is now best-effort, as intended.
- Fixed `SanitizeFileName` not stripping filesystem-invalid characters (e.g. `/`) from ADR/task titles, which could turn a title into an invalid file path and fail record creation.
- Fixed `adr_generate_toc` / `task_generate_toc` (and their CLI equivalents `generate-toc` / `task-toc`) documentation, which incorrectly claimed the generated file lands in the project root next to `adr.config.json`; it actually lands in the parent folder of the configured ADR/tasks folder (e.g. `docs/`).

### Documentation

- Added this CHANGELOG.md and a proper README.md covering functional areas, usage, and generated documentation.

## Earlier

The following capabilities predate this changelog and are not broken out by version:

- Core ADR management: `init`, `new`, `copy`, `list`, `find` (`query`), `link`, `unlink`, `sync`, `generate-toc`, with dual markdown/JSON storage and on-demand template creation.
- Project planning tasks: `new-task`, `list-tasks`, `find-tasks`, `update-task`, `link-task`, `unlink-task`, `generate-task-toc`.
- An MCP (Model Context Protocol) server (`adr-cli mcp`) exposing all of the above as MCP tools with capability parity to the CLI (ADR 00005).
- AI-assisted ADR drafting via Azure AI Foundry (see [AI-Setup.md](AI-Setup.md)).
- File-locking for safe concurrent record updates.
