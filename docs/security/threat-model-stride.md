# STRIDE threat model (v1)

| Category | Threat | Mitigation |
|----------|--------|------------|
| Spoofing | Stolen API keys / JWTs | Hashed API keys, JWT validation, TLS at edge |
| Tampering | Config / transform abuse | Startup validation, transform allowlist |
| Repudiation | Missing audit trail | Correlation IDs, structured logs, traces |
| Information disclosure | Secret leakage | ISecretResolver, no secret logging, config summary redaction |
| Denial of service | Request floods | Local rate limiting, request body limits, timeouts |
| Elevation of privilege | Admin API misuse | AuthN/AuthZ policies, separate admin path |

Residual risk: in-memory rate limits are per-process; multi-instance deployments need sticky routing or a future distributed store.
