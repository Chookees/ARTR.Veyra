# ADR-0006: Layered project boundaries

## Status

Accepted

## Context

The gateway must remain testable and free of circular dependencies.

## Decision

- `ARTR.Veyra.Core` — options, abstractions, validation (no YARP, no ASP.NET hosting)
- `ARTR.Veyra.Infrastructure` — secret resolvers, memory rate store, transform validation helpers
- `ARTR.Veyra.Authentication` — JWT + API key
- `ARTR.Veyra.Observability` — OpenTelemetry + correlation
- `ARTR.Veyra.Host` — composition root, YARP, admin, health

References are acyclic: Host → others → Core.

## Consequences

- Architecture tests enforce boundaries.
- Host is the only project that composes the full pipeline.
