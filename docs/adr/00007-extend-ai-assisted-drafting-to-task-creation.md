# 00007. Extend AI-assisted drafting to task creation

2026-07-22

## Status

__New__

## Context

adr-cli already supports AI-assisted drafting for ADRs (`adr_new --ai` / CLI `--ai`, see AI-Setup.md): drafting Context, Decision, and Consequences from a title, optional user-supplied context, and existing ADRs as grounding, via a configurable AI provider (currently Azure AI Foundry). The same benefit applies to task creation - turning a short title into a structured task instead of requiring the user to write Description and Details by hand.

Unlike a typical AI-assisted workflow, this toolset's task records (`docs/planning`, `TaskRecord`) are themselves the task-tracking system - there is no separate external tracker to publish to. Task drafting is therefore about filling in a task's own fields directly, the same way ADR drafting fills in Context/Decision/Consequences, not about generating content for hand-off to another system.

The configurable ADR root context (ADR 00004) and MCP/CLI capability parity (ADR 00005) both apply equally to task drafting. As adr-cli takes on drafting for both ADRs and Tasks, the distinction between the two record types becomes more important to communicate clearly: an ADR captures a decision and its trade-offs, while a Task describes a concrete unit of work to be done. Tool and command descriptions should make this distinction explicit rather than relying on users (or the AI model) to infer it.

## Decision

Introduce an `ITaskProposalGenerator` interface, parallel to the existing `IAdrProposalGenerator`, rather than generalizing a single shared abstraction across both record types. Each interface's prompt, grounding, and parsing logic stays specific to its own record shape: `TaskProposal` drafts `Description` and `Details`; `AdrProposal` drafts `Context`, `Decision`, and `Consequences`. A shared interface would force an artificial common shape onto two conceptually different records; keeping them parallel favors clarity over reuse, consistent with this project's preference against premature abstraction.

An `AzureFoundryTaskProposalGenerator` implements `ITaskProposalGenerator` the same way `AzureFoundryProposalGenerator` does today: grounded in the task's title, any user-supplied Description, existing task summaries (title/status/description) for consistency, and the task template as a structure/tone example. A `NoOpTaskProposalGenerator` is registered when no AI provider is configured, matching the existing no-op fallback for ADRs.

`ProjectPlanning.NewTaskAsync` gains an `ai` flag, following the same pattern as `AdrNew.CreateDecisionAsync`: a user-supplied Description is preserved rather than overwritten by the draft, and on any AI failure task creation still proceeds from the template. The `task_new` MCP tool and `new-task` CLI command both expose the `ai` flag, keeping MCP/CLI parity as required by ADR 00005.

Tool and command descriptions (MCP tool schemas, CLI help text) for `adr_new`/`new-adr` and `task_new`/`new-task` are updated to state the ADR-vs-Task distinction plainly: an ADR records a decision and its consequences; a Task records a unit of work to be done. This applies to human-facing help text and, where relevant, to the AI system prompt itself, so drafting stays in the right register for each record type.

Acceptance criteria and a definition-of-done are recognized as potentially valuable additions to a task's structure, but are out of scope for this decision - they are not added to `TaskRecord` or `TaskProposal` now, and can be introduced later as a separate, additive change if needed.

## Consequences

Task creation gets the same AI-assisted drafting benefit ADRs already have, using the same configuration (`ai.provider`, `ai.endpoint`, `ai.deploymentName` in `adr.config.json`) and the same failure-tolerant behavior (falls back to template defaults on any AI error).

Maintaining two parallel generator interfaces/implementations duplicates some structure between the ADR and Task AI code paths (prompt-building, template lookup, response parsing). This is an accepted trade-off for clarity over a forced shared abstraction.

Clarifying the ADR-vs-Task distinction in tool descriptions reduces ambiguity for both human users and the AI model about which record type fits a given piece of content, and should reduce cases where drafted content strays into the wrong record's concerns.

Deferring acceptance criteria / definition-of-done keeps this change scoped to the generator and plumbing work; a future ADR can revisit whether and how to add structured completion criteria to tasks.
