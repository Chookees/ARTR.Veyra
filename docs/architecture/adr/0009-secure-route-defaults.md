# ADR-0009: Secure route defaults

## Status

Accepted

## Context

When authentication is enabled, operators may forget to set per-route authorization metadata. A single unannotated YARP route would accept anonymous traffic through an otherwise protected gateway.

## Decision

Introduce `RoutingSecurity.DenyAnonymousRoutesByDefault` (default `true`).

When **both** `Authentication.Enabled` and `DenyAnonymousRoutesByDefault` are true:

- Every `ReverseProxy:Routes` entry must set `Metadata.AllowAnonymous=true` **or** `Metadata.AuthorizationPolicy` to a known policy name (including built-in `VeyraAdmin`)
- `ReverseProxyRouteSecurityValidator` (Core) validates routes at startup; failures throw `InvalidOperationException`
- `ReverseProxyAuthorizationMiddleware` (Host) enforces at runtime: routes without metadata receive **403 Forbidden**; routes with a policy are evaluated via `IAuthorizationService`

Admin plane (`/_veyra/*`) is protected separately via `Admin.RequireAuthentication` and `UseVeyraAdminAudit`, which logs method, path, status, authentication state, and trace ID for every admin request.

## Consequences

- Secure-by-default posture when authentication is on; explicit opt-in required for anonymous routes
- Route authors must coordinate policy names with `Authorization.Policies`
- Deny-by-default can be disabled for migration by setting `DenyAnonymousRoutesByDefault` to `false`
