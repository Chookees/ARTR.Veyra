# Statement of applicability

This document describes which security and operational practices are **in scope** for ARTR Veyra as shipped from this repository. It is a readiness aid for organizations building an ISMS or control program. **ARTR Veyra is not ISO/IEC 27001 certified, SOC 2 attested, or otherwise formally certified.**

## In scope (provided by the product)

- Configurable authentication (JWT, API key) and role-based authorization
- Configuration validation at startup and on reload with last-known-good retention
- Structured error responses (Problem Details) for auth and rate-limit failures
- Health endpoints for liveness, readiness, and startup
- Correlation ID propagation and OpenTelemetry hooks
- YARP transform allowlisting
- Secret resolution abstraction (environment, configuration, file)
- Optional admin listener isolation
- Supply-chain tooling (lock files, audit, CI gates)

## Out of scope (operator or organization responsibility)

- Physical datacenter security
- Organizational policies, training, and background checks
- Formal risk assessment sign-off and audit execution
- TLS certificate issuance and PKI governance (guidance only in runbooks)
- Centralized log retention, SIEM integration, and alerting rules
- Distributed rate limiting across clusters
- Container orchestration, Kubernetes, or Helm charts (explicit non-goal)
- Guaranteed uptime SLAs

## Applicability statement

Deployers decide which controls from [ControlMatrix.md](ControlMatrix.md) apply to their environment. Evidence artifacts are listed in [EvidenceIndex.md](EvidenceIndex.md). Residual risks are tracked in [RiskRegister.md](RiskRegister.md).

For ISO/IEC 27001 mapping notes without certification claims, see [iso27001-readiness.md](iso27001-readiness.md). For SOC 2 readiness notes, see [soc2-readiness.md](soc2-readiness.md).
