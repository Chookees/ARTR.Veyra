# ARTR Veyra

[![CI](https://github.com/Chookees/ARTR.Veyra/actions/workflows/ci.yml/badge.svg)](https://github.com/Chookees/ARTR.Veyra/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Chookees/ARTR.Veyra/actions/workflows/codeql.yml/badge.svg)](https://github.com/Chookees/ARTR.Veyra/actions/workflows/codeql.yml)
[![Release](https://github.com/Chookees/ARTR.Veyra/actions/workflows/release.yml/badge.svg)](https://github.com/Chookees/ARTR.Veyra/actions/workflows/release.yml)
[![Scheduled security](https://github.com/Chookees/ARTR.Veyra/actions/workflows/scheduled-security.yml/badge.svg)](https://github.com/Chookees/ARTR.Veyra/actions/workflows/scheduled-security.yml)
[![Benchmarks](https://github.com/Chookees/ARTR.Veyra/actions/workflows/benchmarks.yml/badge.svg)](https://github.com/Chookees/ARTR.Veyra/actions/workflows/benchmarks.yml)
[![Coverage](https://img.shields.io/badge/coverage-%E2%89%A590%25%20line%20%26%20branch-brightgreen)](coverlet.runsettings)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](global.json)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

**Envoy-inspired API gateway for .NET — self-hosted, zero containers, security-first.**

ARTR Veyra is a Layer-7 gateway on .NET 10 and YARP: route authorization (deny-by-default), canary weights, safe retries (off by default), local rate limits, golden-signal metrics, and DI-only extensions — without Docker, Redis, or a control plane.

> Not Envoy. No xDS, no WASM, no service-mesh claim. Inspiration, not parity.

## Why not just YARP?

| Need | Raw YARP | ARTR Veyra |
|------|----------|------------|
| Secure route defaults | DIY | Deny-by-default + route metadata validation |
| Canary / weights | Raw cluster JSON | First-class `Canary` + schema/validator |
| Safe retries | Easy to get wrong | Idempotent-only, no body replay, default **off** |
| Rate limits | Bring your own | Global + per-route via `IRateLimiterStore` |
| Ops story | You assemble | Admin `/_veyra/*`, diagnostics, Prometheus metrics |
| Extensions | Custom middleware | `IVeyraRequestGate` / `IVeyraProxyObserver` (DI only) |

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build and test

```bash
dotnet restore ARTR.Veyra.sln
dotnet build ARTR.Veyra.sln -c Release --no-restore
dotnet test ARTR.Veyra.sln -c Release --no-build
```

### Run the demo (no containers)

```powershell
.\build\RunDemo.ps1
```

Shows weighted canary (`/canary`), per-route 429 on `/b`, health, diagnostics, and a live 90/10 → 50/50 canary flip via config reload.

```bash
curl http://127.0.0.1:5080/a/hello
curl http://127.0.0.1:5080/canary/hello
curl http://127.0.0.1:5080/_veyra/diagnostics
```

### Configuration

- Config section: `ARTR:Veyra`
- Environment prefix: `ARTR_VEYRA_`
- Admin base path: `/_veyra`
- Example: [`config/veyra.example.json`](config/veyra.example.json)
- Schema: [`config/schemas/veyra.schema.json`](config/schemas/veyra.schema.json)

## Microbenchmarks

Measured on CI (see [benchmarks workflow](.github/workflows/benchmarks.yml)). Numbers below are placeholders until the scheduled job publishes artifacts — **never invent latency figures**. After a local run:

```bash
dotnet run -c Release --project benchmarks/ARTR.Veyra.Benchmarks -- --filter '*'
```

| Benchmark | Notes |
|-----------|--------|
| `ApiKeyHash` | SHA-256 hex for API-key auth |
| `RateLimitPartitionKey` | Partition key composition |
| `CorrelationId_NewGuid` | Correlation id generation |
| `OptionsLookup_Bind` | Options bind hot path |
| `TransformAllowlist_Validate` | Transform allowlist check |
| `RouteSecurity_Validate` | Route security validation |

## Documentation

| Topic | Location |
|-------|----------|
| Usage guide | [docs/HowToUse.md](docs/HowToUse.md) |
| Canary & outlier | [docs/traffic/canary-and-outlier.md](docs/traffic/canary-and-outlier.md) |
| Golden signals | [docs/operations/golden-signals.md](docs/operations/golden-signals.md) |
| Sample deny-list gate | [samples/ARTR.Veyra.Sample.DenyListGate](samples/ARTR.Veyra.Sample.DenyListGate) |
| Architecture ADRs | [docs/architecture/adr](docs/architecture/adr) |
| Security / threat model | [docs/security/threat-model-stride.md](docs/security/threat-model-stride.md) |

## License

Apache-2.0
