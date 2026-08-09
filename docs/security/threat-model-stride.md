# STRIDE threat model (v1)

| Category | Threat | Mitigation |
|----------|--------|------------|
| Spoofing | Stolen API keys / JWTs | Hashed API keys, JWT validation, TLS at edge |
| Tampering | Config / transform abuse | Startup validation, transform allowlist |
| Repudiation | Missing audit trail | Correlation IDs, structured logs, traces |
| Information disclosure | Secret leakage | ISecretResolver, no secret logging, config summary redaction |
| Denial of service | Request floods | Local rate limiting, request body limits, timeouts |
| Elevation of privilege | Admin API misuse | AuthN/AuthZ policies, separate admin path |
| Elevation of privilege | Malicious extension gate | DI-only registration at startup; `Features.Enabled` allowlist when non-empty |
| Elevation of privilege | Untrusted plugin DLL | `Features.Plugins` opt-in; path containment under `AllowedRoot`, SHA-256 hash pinning; no arbitrary script engine or dynamic untrusted loading in v1.2 |

Residual risk: in-memory rate limits are per-process; multi-instance deployments need sticky routing or a future distributed store.
