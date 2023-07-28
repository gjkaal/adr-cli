# 00002. Use SQLite for database baseline

2023-07-28

## Status

__New__

Extrapolates [00001.Record Architecture Decisions initialization](.\00001-record-architecture-decisions-initialization)

References [00001.Record Architecture Decisions initialization](.\00001-record-architecture-decisions-initialization)

## Context

For defining databases, it should enable unit testing, integration testing and portability. Selecting a baseline database with a simple model, helps with these goals.

## Decision

SQLite is out baseline for unit testing and integration testing. SQLServer is the baseline for production.

## Consequences

- Stored procedures and other advanced models may not be available.
- Possibly more work for some advanced data types.



