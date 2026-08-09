# ADR-0012: DI-only extensions

## Status

Accepted

## Context

Gateway extensions (request gates, proxy observers) must integrate without a dynamic script engine or untrusted runtime loading in v1.2. Operators still need opt-in feature toggles and a path toward signed plugin packaging validation.

## Decision

**Request gates and observers — DI only at startup**

- `IVeyraRequestGate` — pre-proxy allow/deny after routing metadata is known
- `IVeyraProxyObserver` — post-proxy observation hook (must not mutate response bodies)
- Registered via `IServiceCollection` in the Host composition root; invoked by `GatewayTelemetryAndExtensionMiddleware`

**Feature allowlist**

- `Features.Enabled` — when non-empty, only gates/observers whose `FeatureId` appears in the list run; when empty, all registered extensions run

**Plugins — separate opt-in**

- `Features.Plugins.Enabled` defaults `false`
- When enabled, `Sha256PluginLoader` validates `Entries` (path containment under `AllowedRoot`, SHA-256 hash match) at startup
- Validation confirms packaging integrity only; v1.2 does **not** execute arbitrary plugin code or load untrusted DLLs dynamically

## Consequences

- Extensions require a trusted build/deploy of the Host (or referenced assemblies registered in DI)
- Signed plugin packaging is a supply-chain control, not a runtime extension mechanism in v1.2
- Future versions may add controlled assembly loading behind the same hash/path pinning model
