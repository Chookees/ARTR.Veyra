# Secrets management

Sensitive values (JWT signing keys, file-backed secrets) are referenced by name and resolved at runtime.

## Secret providers

Configure under `Secrets.Providers`:

| Type | Purpose |
|------|---------|
| `environment` | Read from environment variables |
| `configuration` | Read from `IConfiguration` keys |
| `File` | Read from a file path (`Path` required) |

Providers are composed in registration order. Duplicate provider names are rejected at validation.

## References

Authentication options use secret names such as:

- `env:VEYRA_JWT_SIGNING_KEY` — environment variable
- Configuration-backed names as documented in `config/veyra.example.json`

## Practices

- Never commit plaintext signing keys or API keys to source control
- Store only SHA-256 hashes of API keys in configuration
- Restrict file permissions on secret files and environment files (`/etc/artr-veyra/veyra.env`)
- Rotate keys on a schedule; see [certificate rotation runbook](../operations/runbooks/cert-rotation.md)
- Secret resolution failures throw `SecretResolutionException` at startup when a required secret is missing

## Logging

Secret values are not logged. Configuration summary endpoints expose counts and enabled flags, not secret material.

See ADR-0004 and [authentication](authentication.md).
