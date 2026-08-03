# Health endpoints

Health checks are served under the admin path base (default `/_veyra`).

| Probe | Default path | Purpose |
|-------|--------------|---------|
| Liveness | `/_veyra/health/live` | Process is running |
| Readiness | `/_veyra/health/ready` | Ready to accept traffic |
| Startup | `/_veyra/health/startup` | Startup sequence complete |

Paths are configurable via `Health.LivePath`, `Health.ReadyPath`, and `Health.StartupPath` relative to `Admin.PathBase`.

## Disabling health

Set `Health.Enabled` to `false` to unmap health endpoints (not recommended for production).

## Load balancers

- Use **liveness** to restart unhealthy instances
- Use **readiness** to remove instances from rotation during dependency outages
- **Startup** helps slow-start scenarios before marking ready

## Authentication

When `Admin.RequireAuthentication` is true and authentication is enabled, health endpoints follow the same admin auth policy unless your deployment uses a dedicated admin listener with network isolation.

## Response

Successful probes return **200 OK** with plain or minimal body per ASP.NET health check defaults.

See [deployment](deployment.md) and [troubleshooting](troubleshooting.md).
