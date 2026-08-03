# Configuration reload

ARTR Veyra supports hot reload of configuration files when `ConfigurationReload.Enabled` is `true` (default).

## What reloads

File providers registered in the host reload when their underlying files change:

- `appsettings.json` and environment-specific variants
- `config/veyra*.json`

Environment variables and command-line values are not re-read on file change alone; they apply at process start unless your deployment triggers a full configuration rebuild.

## Activation pipeline

`ConfigurationActivationService` (Infrastructure layer) listens for configuration change tokens and:

1. Validates the candidate `VeyraOptions` via `VeyraOptionsValidator`
2. On success: increments `configuration.generation`, updates `configuration.fingerprint`, records `lastActivatedUtc`
3. On failure: increments the `veyra_config_activation_failures_total` metric, logs a warning, and retains last-known-good when `RetainLastKnownGood` is `true`

Invalid configuration at **startup** fails the process. Invalid configuration on **reload** does not terminate the process.

## Inspecting state

```http
GET /_veyra/config/summary
```

Response includes:

```json
"configuration": {
  "generation": 1,
  "fingerprint": "<sha256-hex>",
  "lastKnownGoodActive": false,
  "lastActivatedUtc": "2026-08-03T00:00:00Z"
}
```

When `lastKnownGoodActive` is `true`, the running options may differ from the file on disk because the latest reload was rejected.

## Operational guidance

- Validate changes in a staging environment before editing production files
- Watch activation failure metrics and logs after deploys
- For breaking changes (listener URLs, TLS certificates), prefer a controlled restart over relying on reload
- Disable reload with `ConfigurationReload.Enabled: false` when configuration is immutable for the process lifetime

See [configuration reference](reference.md) and [operations deployment](../operations/deployment.md).
