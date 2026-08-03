# Control matrix

Mapping of common control themes to ARTR Veyra capabilities. This matrix supports internal governance; **it does not assert ISO/IEC 27001, SOC 2, or any other certification**.

| Control theme | Objective | Veyra implementation | Evidence location |
|---------------|-----------|----------------------|-------------------|
| AC-01 Access enforcement | Restrict admin and route access | JWT, API key, authorization policies | `docs/security/authentication.md`, `docs/security/authorization.md` |
| AC-02 Least privilege | Role-based access | `Authorization.Policies` | `docs/configuration/reference.md` |
| CM-01 Configuration management | Validated config | `VeyraOptionsValidator`, activation service | `docs/configuration/reload.md`, ADR-0008 |
| CM-02 Change detection | Safe reload | Last-known-good on failed reload | `ConfigurationActivationService` |
| IR-01 Incident response | Operational playbooks | Runbooks | `docs/operations/runbooks/incident.md` |
| LG-01 Logging | Traceability | Correlation IDs, OpenTelemetry | `docs/operations/observability.md` |
| SC-01 Supply chain | Dependency risk | Lock files, NuGet audit, CodeQL | `docs/security/supply-chain.md` |
| SC-02 Secrets | Protect credentials | Secret resolvers, hashed API keys | `docs/security/secrets.md` |
| AV-01 Availability | Health signaling | Live/ready/startup probes | `docs/operations/health.md` |
| NET-01 Network segmentation | Admin isolation | `Admin.ListenUrls` | ADR-0007, `docs/operations/deployment.md` |

Organizations map these rows to their own control frameworks and collect evidence during audits.
