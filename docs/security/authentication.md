# Authentication

ARTR Veyra supports optional authentication for the admin plane and can protect proxied routes when combined with authorization policies.

## Schemes

### JWT Bearer

Enable with `Authentication.Jwt.Enabled`. Configure either:

- **OIDC authority** — `Authority` (and optional `MetadataAddress`, `Audience`, `Issuer`)
- **Symmetric key** — `SigningKeySecretName` referencing a resolved secret (e.g. `env:VEYRA_JWT_SIGNING_KEY`)

Clients send `Authorization: Bearer <token>`. Invalid or expired tokens return **401 Unauthorized** with `application/problem+json` and error code `VEYRA_AUTH_INVALID`.

### API key

Enable with `Authentication.ApiKey.Enabled`. Keys are stored as SHA-256 hashes (`HashSha256Hex`); plaintext keys are never persisted in configuration.

Clients send the configured header (default `X-Api-Key`). Missing or invalid keys return **401** with `VEYRA_AUTH_INVALID`.

## Dual scheme selection

When both JWT and API key are enabled, a policy scheme selects the handler based on request headers:

- `X-Api-Key` (or configured header) present → API key handler
- `Authorization: Bearer` present → JWT handler

## Admin protection

`Admin.RequireAuthentication` defaults to `true`. When authentication is enabled and this flag is set, all admin endpoints require a valid credential unless explicitly allowed anonymous in development tooling.

## Failure responses

Authentication failures use RFC 7807 Problem Details. The host middleware ensures consistent `Content-Type` and extension fields including `errorCode`.

## Secret handling

Signing keys and API key hashes are resolved through the secret provider pipeline. Secret values are not written to logs or exposed in `/config/summary`.

See [authorization](authorization.md) and [secrets](secrets.md).
