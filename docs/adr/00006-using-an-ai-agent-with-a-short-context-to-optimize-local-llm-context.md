# 00006. Using an AI agent with a short context to optimize local LLM context

2026-07-22

## Status

__New__

## Context

Local LLMs often have smaller context windows and more limited inference capacity than hosted models. Supplying the full repository context, complete ADR history, or unfiltered tool output can exceed those limits, increase latency, and reduce response quality by obscuring relevant information.

We need a consistent way to select, summarize, and structure only the information required for a task. The configured ADR root context established by ADR 00004 remains the source boundary for ADR-related operations, while the equivalent MCP and CLI capabilities required by ADR 00005 must remain available. Context optimization should therefore be independent of how a request is initiated.

## Decision

We will use an AI agent with a deliberately short working context to prepare input for the local LLM. The agent will identify task-relevant repository content, retrieve additional information only when needed, and produce a compact, structured context containing the request, applicable constraints, relevant ADRs, and necessary source excerpts.

The agent will respect the configured ADR root context and expose the same context-optimization behavior through both MCP tools and CLI commands. It will prefer traceable extraction and concise summarization over including entire files or histories. The local LLM will receive the optimized context together with references to the source material used to construct it.

## Consequences

Local LLM requests will use fewer tokens, require less memory, and generally have lower latency. Relevant constraints and prior decisions will be more prominent, which should improve output quality for models with limited context windows.

The additional agent step introduces processing overhead, implementation complexity, and another source of nondeterminism. Summarization or relevance filtering may omit important details or distort their meaning, so source references must be retained and callers must be able to inspect the selected context. Tasks requiring exact wording or broad repository analysis may need direct excerpts or a larger context rather than aggressive summarization.

Context selection and optimization logic must be maintained consistently across MCP and CLI interfaces and covered by unit tests. The approach does not replace the configured ADR root boundary or change existing ADR-linking practices; it determines how information within those boundaries is prepared for a local LLM.
