# 00013. Structural well-formedness check for AI-drafted Consequences must verify list contents, not just headers

2026-08-05

## Status

__Accepted__

## Context

The AI proposal generator (Adr.Cli.Ai.AzureFoundry.AzureFoundryProposalGenerator) drafts Context, Decision, and Consequences as three separate structured-output calls, then runs a local IsWellFormed check before accepting the result - rejecting empty fields, a Decision that merely restates Context, or a Consequences block missing a Pro's/Con's section entirely. That check only looked for the "Pro's:"/"Con's:" header substrings in the formatted Consequences text. FormatConsequences always emits both headers even when one list (pros or cons) is empty - e.g. an empty cons array still renders as "*Pro's:*\n- a\n\n*Con's:*\n" with no items under Con's. This is schema-valid (the JSON schema doesn't require non-empty arrays) and passed IsWellFormed, so a real user-visible symptom was ADRs created with ai:true ending up with a Consequences section that had a header but no actual content under it - functionally the same failure as an empty field, just disguised by the header text being present.

Separately, CompleteAsync never inspected the chat completion's FinishReason, so a response truncated by a token limit or cut short by a content filter could still parse as valid (but incomplete) JSON and be silently accepted as a successful draft.

## Decision

Validate AI-generated proposal sections from their structured-output values before formatting. Both the pros and cons collections must contain at least one non-blank entry; rendered headers alone do not satisfy this requirement. `CompleteAsync` must also accept a completion only when its `FinishReason` indicates normal completion. Any incomplete completion, invalid payload, or empty required collection is treated as generation failure so that malformed drafts are not persisted or presented as successful.

## Consequences

*Pro's:*
- Prevents Consequences sections with empty pros or cons from being accepted.
- Rejects blank list entries that provide no substantive consequence.
- Prevents truncated or filtered completions from being treated as successful drafts.
- Keeps malformed AI-generated proposals from being persisted or presented to users.
- Makes validation independent of formatting details such as rendered section headers.
- Provides consistent failure handling for incomplete completions, invalid payloads, and missing required content.

*Con's:*
- More AI generation attempts will fail when providers return incomplete metadata or structurally insufficient content.
- Validation becomes coupled to the provider's completion-status semantics (normalize provider-specific finish reasons at the integration boundary).
- Existing tests and mocks must supply structured list values and successful finish reasons.
- Users may receive a generation failure instead of a partially usable draft (provide clear retry guidance).
