# Observability

## Correlation IDs

Every request receives or preserves the `X-Correlation-ID` header. The value is echoed on responses and included in structured logs when console logging is enabled.

## Logging

`Observability.ConsoleLogging` controls console output. Production deployments typically pair console logs with a log shipper or disable console logging when using OTLP.

## OpenTelemetry

When `Observability.Otlp.Enabled` is `true`, traces and metrics export to `Observability.Otlp.Endpoint` (gRPC OTLP).

Instrumentation includes:

- ASP.NET Core requests
- Outbound HTTP (YARP upstream calls)
- Custom meter `ARTR.Veyra`:
  - `veyra.requests.total`, `veyra.auth.failures.total`, `veyra.ratelimit.exceeded.total`, `veyra.proxy.errors.total`
  - `veyra.destinations.healthy`, `veyra.destinations.ejected` (gauges)
  - `veyra_config_activation_failures_total`

## Prometheus

`Observability.Prometheus.Enabled` exposes a scrape endpoint at `{Admin.PathBase}{Prometheus.Path}`. The exporter package is **beta** (ADR-0005); validate in non-production before relying on it.

## Admin introspection

| Endpoint | Data |
|----------|------|
| `GET /_veyra/info` | Product name, version, feature flags |
| `GET /_veyra/config/summary` | Non-secret configuration summary and activation generation/fingerprint |
| `GET /_veyra/diagnostics` | Clusters, destination weights, passive health, routing security flags |

## Dashboards

Correlate gateway logs and traces using `X-Correlation-ID`. Activation failures increment `veyra_config_activation_failures_total` — alert on sustained increases after configuration changes.

See [golden signals](golden-signals.md), [health](health.md), and [configuration reload](../configuration/reload.md).
