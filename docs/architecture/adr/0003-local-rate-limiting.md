# ADR-0003: Local rate limiting with store abstraction

## Status

Accepted

## Context

v1 must rate-limit without Redis or other external stores.

## Decision

Use ASP.NET Core rate limiting middleware with in-memory state for the default path, and expose `IRateLimiterStore` for a future distributed implementation.

## Consequences

- Limits are per process; horizontal scale requires sticky routing or a future distributed store.
- Default run and tests need no external services.
