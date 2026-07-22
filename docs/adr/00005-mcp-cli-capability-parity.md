# 00005. MCP tools and CLI commands should offer equivalent capabilities

2026-07-22

## Status

__Accepted__

## Context

While implementing ADR 00004 (`adr_set_context` / `adr_get_context` as MCP tools), the first pass
also added a literal CLI mirror: a `set-context --workingDirectory <path>` command alongside a
`context` command. That was wrong, and caught only because it was pointed out before it shipped:
a CLI invocation is a fresh process every time, and `AdrSettings` already resolves its config by
searching upward from that process's actual current directory - correctly, for the CLI's execution
model. A `set-context` command would only mutate the in-memory `IAdrSettings` singleton for the
remainder of that one process's single command, which then exits. It could never affect a later,
separate `adr-cli` invocation, because there is no persisted or shared state between CLI
invocations (and, per ADR 00004, deliberately shouldn't be - persisting it reintroduces the
`settings.Write()` config-corruption risk documented there). So the command would parse
successfully, print a success message, and do precisely nothing useful. The "set" side only means
something for the MCP server, which is long-lived and pins its config once at startup regardless of
which repository the calling session is actually working in.

The read side did not have this problem: "what does adr-cli currently resolve to from here" is a
meaningful question in both a long-lived MCP session and a single CLI invocation, so
`adr_get_context` and a CLI `context` command both call through the same `IAdrContext` command
handler with no adjustment needed.

## Decision

When a capability is added to one interface (an MCP tool or a CLI command), check whether it should
also be exposed through the other - but "parity" means the same *capability* is reachable from
both, not that every command must exist in both places with an identical shape. Before mirroring a
command, check whether its semantics actually survive the transfer:

- A capability that only reads or reports state (e.g. "what config is active") usually transfers
  directly, because both interfaces can answer it from their own current, real state.
- A capability that sets or pins state for the remainder of a *session* only makes sense where a
  session outlives a single call - true for a long-lived MCP server process, not for a CLI
  invocation that starts, runs one command, and exits. Don't add a CLI command whose only effect is
  to mutate in-memory state that the process is about to discard anyway.
- When an interface already gets a capability "for free" from its own execution model (the CLI
  getting the correct working directory from its real process cwd on every invocation), don't add a
  command whose entire job is to re-provide something that interface already has natively.

Concretely for ADR 00004: `adr_get_context` (MCP) and `adr-cli context` (CLI) both exist, and both
delegate to `IAdrContext.GetContextAsync()`. `adr_set_context` remains MCP-only, with the reasoning
above recorded in `AdrContextSetup.GetContextCommand`'s remarks so it isn't quietly "completed"
later by someone assuming the pair should be symmetric.

## Consequences

- Adding a new MCP tool should prompt checking whether the equivalent CLI command exists or is
  worth adding, and vice versa - this is now a standing check, not a one-off.
- The check is capability parity, not command parity: an interface-specific reason to skip the
  mirror (as with `set-context` above) is an acceptable, expected outcome, not a gap to fill in
  later. That reasoning should be written down at the skipped site so it survives beyond the
  conversation that produced it.
