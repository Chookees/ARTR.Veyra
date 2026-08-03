# Authorization

Authorization builds on authenticated identities (JWT claims or API key roles).

## Configuration

```json
"Authorization": {
  "Enabled": true,
  "Policies": {
    "reader": ["read"],
    "admin": ["admin"]
  }
}
```

Each policy name maps to one or more role names. Users must hold at least one listed role.

## Admin policy

The host registers a built-in admin policy (`VeyraAdmin`) used when `Admin.RequireAuthentication` is true. Valid credentials with appropriate roles satisfy the policy.

## Route-level policies

YARP routes can reference ASP.NET authorization policies when configured in the reverse proxy pipeline. Policy names must match entries under `Authorization.Policies`.

## Fail-closed behavior

When authorization is enabled, unauthenticated requests receive **401**. Authenticated requests lacking required roles receive **403 Forbidden** with Problem Details.

## Relationship to authentication

Authorization has no effect when `Authentication.Enabled` is `false`. Enable at least one authentication scheme before turning on authorization.

See [authentication](authentication.md).
