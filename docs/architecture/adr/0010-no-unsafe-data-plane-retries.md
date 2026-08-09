# ADR-0010: No unsafe data-plane retries

## Status

Accepted

## Context

Automatic retries on the outbound data plane can duplicate side effects (non-idempotent methods, request bodies) or mask TLS misconfiguration. Operators still need optional, bounded retries for transient upstream failures on safe requests.

## Decision

Add `TrafficEngineering.SafeRetries` with **`Enabled` default `false`**.

When enabled, `SafeRetryForwarderHttpClientFactory` wraps YARP's forwarder handler with `SafeRetryHandler`:

- Retries only HTTP methods listed in `IdempotentMethods` (default `GET`, `HEAD`, `OPTIONS`); `POST`, `PATCH`, and `CONNECT` are rejected at validation
- Never retries requests that carry a body (`request.Content is not null`)
- Bounded by `MaxAttempts` (1–5) and `TotalTimeoutSeconds` (1–120) across all attempts
- Retries on transport failures and **502**, **503**, **504** only

`SafeRetryForwarderHttpClientFactory` preserves YARP's default TLS handler chain. **Remote certificate validation is never disabled.**

## Consequences

- Default behavior matches YARP single-attempt forwarding
- Operators must explicitly opt in and tune idempotent method allowlists
- Body-bearing idempotent requests (unusual) are not retried
