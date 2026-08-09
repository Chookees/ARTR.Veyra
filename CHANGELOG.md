# Changelog

All notable changes to ARTR Veyra are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-08-06

### Added

- Opt-in signed plugin packaging validation (`Features:Plugins`) with AllowedRoot pinning, SHA-256 verification, and rejection of temporary/world-writable roots (no arbitrary script engine).
- Scheduled BenchmarkDotNet CI workflow with soft regression gates and README benchmark table hooks (measured values only).
- Hedging remains config-reserved and rejected when enabled until a safe implementation ships.

## [1.2.0] - 2026-08-06

### Added

- First-class canary splits (`Canary.Splits` weights must sum to 100) applied via YARP config filter; header-match documentation pattern.
- Outlier detection options map to YARP passive health; `veyra.destinations.healthy` / `veyra.destinations.ejected` gauges.
- DI-only extension seams `IVeyraRequestGate` / `IVeyraProxyObserver` with optional `Features:Enabled` allowlist.
- Sample deny-list gate under `samples/ARTR.Veyra.Sample.DenyListGate`.
- Bounded `GET /_veyra/diagnostics` and golden-signals operations docs + Grafana JSON.

## [1.1.0] - 2026-08-06

### Added

- YARP route authorization middleware + deny-by-default when authentication is enabled (`RoutingSecurity.DenyAnonymousRoutesByDefault`).
- Admin audit structured logs for `/_veyra/*`.
- Wired `veyra.requests|auth.failures|ratelimit.exceeded|proxy.errors` counters on real request paths.
- First-class traffic engineering options: health/LB defaults, destination weights validation, safe retries (default off).
- Per-route rate limits via `IRateLimiterStore` and route `Metadata.RateLimitPolicy`.
- Killer `RunDemo.ps1` (canary, 429, diagnostics, reload flip), expanded microbenchmarks, Envoy-inspired README positioning.
- ADRs 0009–0011; schema extensions for RoutingSecurity / TrafficEngineering / Canary / Features.

## [1.0.0] - 2026-08-03

### Added

- Initial open-source release of ARTR Veyra: YARP-based L7 API gateway with JWT and API-key authentication, authorization policies, local rate limiting, transform allowlist validation, secret resolution, OpenTelemetry observability, admin API under `/_veyra`, health probes, native publish scripts, Windows Service and systemd units, demo samples, and CI/release workflows without containers.
