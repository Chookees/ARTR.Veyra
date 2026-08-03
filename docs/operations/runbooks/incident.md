# Runbook: Incident

1. Capture correlation IDs from client reports (`X-Correlation-ID`).
2. Check live/ready endpoints and recent logs.
3. Inspect rate-limit and auth failure metrics.
4. If upstreams fail, verify destination health independently of the gateway.
5. Roll back to the previous published artifact if a release caused the incident.
6. Record timeline and follow-up actions.
