# 00004. Container setup is not done by injecting classes based on a basetype

2023-08-09

## Status

__Accepted__

## Context

The project uses dependency injection for setting up the command handlers.

## Decision

This could be done using some base class or interface and inject every element that complies to that class or interface. This method of container configuration
is used by several DI extensions, but could be viewed as an anti-pattern. In this application, during startup, command handlers and otrher services are injected
individually.

## Consequences

The initialization may be larger and more complex, but it gives better insight in the services that are actually part of the application setup.
