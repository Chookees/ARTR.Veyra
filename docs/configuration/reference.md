# Configuration reference

Root section: `ARTR:Veyra`  
Proxy section: `ReverseProxy`  
Environment prefix: `ARTR_VEYRA_` (nested keys use `__`, e.g. `ARTR_VEYRA_Admin__PathBase`)

JSON Schema: `config/schemas/veyra.schema.json`  
Example: `config/veyra.example.json`

## Admin

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Admin.Enabled` | bool | `true` | Expose admin endpoints |
| `Admin.PathBase` | string | `/_veyra` | Absolute path prefix for admin plane |
| `Admin.RequireAuthentication` | bool | `true` | Require auth for admin when authentication is enabled |
| `Admin.ListenUrls` | string? | `null` | Semicolon-separated dedicated listener URLs for admin only |

When `ListenUrls` is set, Kestrel binds data-plane URLs from `Urls` plus admin URLs. Admin paths are only served on admin listeners; data-plane listeners return 404 for admin paths.

## Authentication

| Key | Type | Description |
|-----|------|-------------|
| `Authentication.Enabled` | bool | Master switch |
| `Authentication.Jwt.Enabled` | bool | JWT bearer validation |
| `Authentication.Jwt.Authority` | string? | OIDC authority (absolute URI) |
| `Authentication.Jwt.SigningKeySecretName` | string? | Secret reference for symmetric signing key |
| `Authentication.ApiKey.Enabled` | bool | API key validation |
| `Authentication.ApiKey.HeaderName` | string | Header carrying the key (default `X-Api-Key`) |
| `Authentication.ApiKey.Keys` | array | Key entries with `Id`, `Name`, `HashSha256Hex`, optional `Roles` |

## Authorization

| Key | Type | Description |
|-----|------|-------------|
| `Authorization.Enabled` | bool | Enable role-based policies |
| `Authorization.Policies` | object | Map of policy name → role array |

## Rate limiting

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `RateLimiting.Enabled` | bool | `false` | Enable ASP.NET rate limiter |
| `RateLimiting.GlobalPermitLimit` | int | `100` | Global permit count per window |
| `RateLimiting.GlobalWindowSeconds` | int | `60` | Global window duration |
| `RateLimiting.Policies` | array | `[]` | Named policies with `PermitLimit`, `WindowSeconds`, `QueueLimit` |

## Observability

| Key | Type | Description |
|-----|------|-------------|
| `Observability.ServiceName` | string | OpenTelemetry service name |
| `Observability.ConsoleLogging` | bool | Console log output |
| `Observability.Otlp.Enabled` | bool | OTLP trace/metric export |
| `Observability.Otlp.Endpoint` | string | OTLP collector URI |
| `Observability.Prometheus.Enabled` | bool | Prometheus scrape endpoint (beta) |
| `Observability.Prometheus.Path` | string | Path under admin base |

## Health

| Key | Type | Default |
|-----|------|---------|
| `Health.Enabled` | bool | `true` |
| `Health.LivePath` | string | `/health/live` |
| `Health.ReadyPath` | string | `/health/ready` |
| `Health.StartupPath` | string | `/health/startup` |

Paths are relative to `Admin.PathBase`.

## Configuration reload

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ConfigurationReload.Enabled` | bool | `true` | React to configuration file changes |
| `ConfigurationReload.RetainLastKnownGood` | bool | `true` | Keep prior valid config when reload validation fails |

Activation state is exposed at `GET {Admin.PathBase}/config/summary` under `configuration`.

## Reverse proxy

YARP routes and clusters are configured under `ReverseProxy` per [YARP documentation](https://microsoft.github.io/reverse-proxy/). Transforms are validated against an allowlist before startup.

## Validation

`VeyraOptionsValidator` runs at startup and on each configuration activation. Invalid startup configuration fails fast. Invalid reload is rejected and last-known-good is retained when enabled.

See also: [environment variables](environment-variables.md), [reload](reload.md).
