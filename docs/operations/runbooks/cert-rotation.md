# Runbook: Certificate rotation

1. Place the new certificate where the process or edge terminator expects it.
2. Update TLS configuration (Kestrel cert path or edge proxy).
3. Reload or restart ARTR Veyra.
4. Verify HTTPS negotiation and `/_veyra/health/live`.
5. Retire the old certificate only after validation.
