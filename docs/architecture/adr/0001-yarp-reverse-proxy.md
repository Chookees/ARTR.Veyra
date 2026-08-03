# ADR-0001: YARP as the reverse proxy engine

## Status

Accepted

## Context

ARTR Veyra must provide production-grade L7 reverse proxying on .NET 10 without containers or external proxy daemons.

## Decision

Use **Yarp.ReverseProxy 2.3.0** as the forwarding engine, configured via the standard `ReverseProxy` configuration section with reload support.

## Consequences

- Gains HTTP/1.1 and HTTP/2 streaming, load balancing, transforms, and ASP.NET integration.
- Host project owns YARP references; Core remains free of YARP.
- Operators learn YARP route/cluster vocabulary.
