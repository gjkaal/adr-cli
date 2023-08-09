# 00003. Command handlers and command definitions are separated

2023-08-09

## Status

__New__

## Context

The ADR CLI application uses the command structure for defining commands and linking them to executable code. The actual code is implemented in classes that can have state and are managed using 
Dependency injection. This code can and should be independent of the system.command structure for testing and reuse purposes.

## Decision

The command definition and the handler implementation are is separate files. The command options have their own class as well, so it is easier to reuse options and to have a better overview of
options available. This prevents the redefinition of options that are more or less alike.

## Consequences

Command structure and command implementation are separated and it can be more difficult to link options, commands and implementation together. On the plus side, it is easier to reuse
implementations from handlers in other handlers.
