# ADR-0004: Secret resolution via ISecretResolver

## Status

Accepted

## Context

Signing keys and other secrets must not be hard-coded and should support environment, configuration, and file sources.

## Decision

Introduce `ISecretResolver` with a composite implementation over environment variables, configuration keys, and files. Configuration references secrets by name; resolvers never log secret values.

## Consequences

- Clear extension point for future vault providers.
- Operators can keep secrets out of committed JSON.
