# Rate limiting

ARTR Veyra uses the ASP.NET Core rate limiting middleware with an in-memory store per process.

## Global limiter

When `RateLimiting.Enabled` is `true`, a global fixed-window limiter applies using:

- `GlobalPermitLimit` — requests allowed per window
- `GlobalWindowSeconds` — window duration

Exceeded requests return **429 Too Many Requests** with Problem Details error code `VEYRA_RATE_LIMITED`.

## Named policies

Additional policies under `RateLimiting.Policies` can be attached to routes or endpoints. Each policy specifies `Name`, `PermitLimit`, `WindowSeconds`, and optional `QueueLimit`.

## Limitations

- Limits are **per process**. Multiple instances do not share counters unless a custom `IRateLimiterStore` is introduced.
- In-memory state is lost on restart.
- Very high traffic may require tuning window sizes and permit limits.

See ADR-0003 (local rate limiting) and [configuration reference](../configuration/reference.md).
