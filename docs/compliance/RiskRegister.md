# Risk register

Known risks and mitigations for ARTR Veyra deployments. Review periodically; this is not a formal enterprise risk register.

| ID | Risk | Likelihood | Impact | Mitigation | Residual |
|----|------|------------|--------|------------|----------|
| R-01 | In-memory rate limits bypassed by multi-instance fan-out | Medium | Medium | Per-instance limits; document scale-out behavior; future distributed store | Per-process counters only |
| R-02 | Prometheus exporter beta instability | Low | Low | ADR-0005; disable in production until validated | Beta package |
| R-03 | Misconfigured forwarded headers enable IP spoofing | Medium | High | Restrict `KnownProxies`/`KnownNetworks`; terminate TLS at trusted edge | Operator config dependent |
| R-04 | API key hash collision or weak keys | Low | High | Use strong keys; rotate; store only SHA-256 hashes | Operator key hygiene |
| R-05 | JWT signing key exposure | Low | Critical | Secret providers; env files with restricted permissions | Deployment practice |
| R-06 | Invalid reload silently ignored | Medium | Medium | `lastKnownGoodActive` in config summary; activation failure metric | Monitor dashboards |
| R-07 | Admin plane exposed on data-plane port | Medium | High | `Admin.ListenUrls` + firewall; require authentication | Network + auth layers |
| R-08 | Upstream compromise via open routes | Medium | High | Authentication/authorization on routes; transform allowlist | Route-level policy |
| R-09 | Dependency vulnerability | Medium | Medium | NuGet audit, lock files, automated scanning | Zero-day window |
| R-10 | No HA/coordination built-in | Medium | Medium | Run multiple instances behind load balancer; external health checks | Single-process design |

Update this register when ADRs or architecture change.
