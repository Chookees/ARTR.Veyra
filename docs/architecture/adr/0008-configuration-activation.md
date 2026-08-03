# ADR-0008: Configuration activation

## Status

Accepted

## Context

Configuration files can reload at runtime (`reloadOnChange: true`). Applying invalid configuration to a live gateway could break routing, authentication, or listeners. Operators need visibility into which configuration generation is active.

## Decision

Introduce `ConfigurationActivationService` in Infrastructure:

- Validates candidate `VeyraOptions` on startup and on configuration change
- On success: increments `generation`, computes SHA-256 `fingerprint` of serialized options, updates `lastActivatedUtc`
- On reload failure: logs warning, increments `veyra_config_activation_failures_total`, retains last-known-good when `ConfigurationReload.RetainLastKnownGood` is true
- On startup failure: throws `OptionsValidationException` (fail fast)

`GET /_veyra/config/summary` exposes `configuration.generation`, `fingerprint`, `lastKnownGoodActive`, and `lastActivatedUtc`.

Options:

- `ConfigurationReload.Enabled` (default `true`)
- `ConfigurationReload.RetainLastKnownGood` (default `true`)

## Consequences

- Safe reload for most option changes; listener URL changes may still require restart
- Operators must monitor activation failures after config edits
- Unit and integration tests cover activation and summary shape
