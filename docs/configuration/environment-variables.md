# Environment variables

Veyra reads configuration from JSON files, environment variables, and command-line arguments. Environment variables override file values when both are present.

## Prefix

All Veyra-specific keys use the prefix `ARTR_VEYRA_`. Nested JSON keys map to double underscores:

| JSON path | Environment variable |
|-----------|---------------------|
| `ARTR:Veyra:Admin:PathBase` | `ARTR_VEYRA_Admin__PathBase` |
| `ARTR:Veyra:Authentication:Jwt:Issuer` | `ARTR_VEYRA_Authentication__Jwt__Issuer` |
| `ARTR:Veyra:RateLimiting:GlobalPermitLimit` | `ARTR_VEYRA_RateLimiting__GlobalPermitLimit` |

Array indices use numeric segments: `ARTR_VEYRA_Authentication__ApiKey__Keys__0__Id`.

## Host binding

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_URLS` / `Urls` | Semicolon-separated data-plane listener URLs |
| `ASPNETCORE_ENVIRONMENT` | Hosting environment (`Production`, `Development`, `Testing`) |
| `DOTNET_ENVIRONMENT` | Same as above for generic host |

## Secret references

JWT signing keys and other secrets can reference environment variables via secret names such as `env:VEYRA_JWT_SIGNING_KEY`. The variable name after `env:` is read at runtime and never logged.

## Linux systemd

Use `EnvironmentFile=-/etc/artr-veyra/veyra.env` in the unit file (see `deploy/linux/systemd/artr-veyra.service`) to load a file of `KEY=value` pairs without embedding secrets in the unit.

## Windows Service

Set environment variables on the service or machine scope before starting the host. Prefer dedicated secret stores for production signing keys and API key material.

## Boolean and numeric values

Use standard .NET configuration binding: `true`/`false`, integer strings for numbers. Empty strings for optional values are treated as unset where applicable.

See [configuration reference](reference.md) for the full key list.
