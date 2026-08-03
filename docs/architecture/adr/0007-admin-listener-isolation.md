# ADR-0007: Admin listener isolation

## Status

Accepted

## Context

Admin endpoints (`/_veyra/*`) expose configuration summaries, health, and optional metrics. On the same listener as proxied traffic, admin paths may be reachable by anyone who can hit the data plane unless network policy or authentication blocks them.

## Decision

Add optional `Admin.ListenUrls` — semicolon-separated Kestrel URLs dedicated to the admin plane.

- When set, the host binds both `Urls` (data plane) and `Admin.ListenUrls`
- Middleware `UseVeyraAdminListenerIsolation` returns 404 when:
  - A request on an admin listener targets a non-admin path
  - A request on a data-plane listener targets an admin path

`VeyraOptionsValidator` validates each URL in `ListenUrls` as an absolute `http` or `https` URI.

## Consequences

- Operators can firewall admin ports separately
- Dual-listener setups require correct `Urls` and `ListenUrls` configuration
- Integration tests continue to use a single listener unless explicitly configured
