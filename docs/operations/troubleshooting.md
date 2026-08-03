# Troubleshooting

## Host fails at startup

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Options validation exception | Invalid `ARTR:Veyra` or `ReverseProxy` config | Fix keys per [configuration reference](../configuration/reference.md); run with `Development` for detailed errors |
| Transform allowlist error | Disallowed YARP transform | Use only allowlisted transforms or adjust `Transforms.Allowlist` |
| Secret resolution error | Missing env var or file for `SigningKeySecretName` | Set referenced secret; verify `Secrets.Providers` |
| Port bind failure | `Urls` or `Admin.ListenUrls` conflict | Change ports; check `ss`/`netstat` |

## 401 on admin endpoints

- Confirm `Authentication.Enabled` and `Admin.RequireAuthentication`
- API key: verify header name and SHA-256 hash matches plaintext key
- JWT: verify issuer, signing key, and token expiry
- Response body should include `VEYRA_AUTH_INVALID` in Problem Details

## 404 on admin paths

- Wrong `Admin.PathBase`
- Request hit data-plane listener while admin is isolated on `Admin.ListenUrls`
- Admin disabled (`Admin.Enabled: false`)

## 429 rate limited

- Lower traffic or raise `GlobalPermitLimit` / policy limits
- Remember limits are per process (ADR-0003)

## Configuration reload not applied

- Check `ConfigurationReload.Enabled`
- Inspect `GET /_veyra/config/summary` — `lastKnownGoodActive: true` means last reload failed validation
- Review logs for activation warnings and `veyra_config_activation_failures_total`

## Upstream errors

- Verify `ReverseProxy` cluster addresses and network connectivity
- Check YARP transform paths
- Use correlation ID to trace request in logs

## Runbooks

- [Restart](runbooks/restart.md)
- [Certificate rotation](runbooks/cert-rotation.md)
- [Incident response](runbooks/incident.md)

Report defects via GitHub issues; security issues via [SECURITY.md](../../SECURITY.md).
