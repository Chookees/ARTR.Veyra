# Golden signals

Veyra exposes four request counters and two destination gauges on meter `ARTR.Veyra`. Scrape via Prometheus (`Observability.Prometheus.Enabled`) or export through OTLP.

## Metrics

| Meter name | Type | Description |
|------------|------|-------------|
| `veyra.requests.total` | Counter | All gateway requests |
| `veyra.auth.failures.total` | Counter | Responses with 401 or 403 |
| `veyra.ratelimit.exceeded.total` | Counter | Requests rejected by rate limiting (429) |
| `veyra.proxy.errors.total` | Counter | 5xx and 502/503/504 proxy failures |
| `veyra.destinations.healthy` | Gauge | Destinations not ejected by passive health |
| `veyra.destinations.ejected` | Gauge | Destinations ejected (passive `Unhealthy`) |

Prometheus export typically sanitizes dots to underscores (e.g. `veyra_requests_total`).

## Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /_veyra/diagnostics` | Runtime snapshot: config generation, feature flags, cluster LB policy, destination weights, passive health state |
| `GET /_veyra/metrics` | Prometheus scrape (default path; `{Admin.PathBase}{Prometheus.Path}`) |

## Example PromQL

```promql
# Request rate (5m)
rate(veyra_requests_total[5m])

# Auth failure ratio
rate(veyra_auth_failures_total[5m]) / rate(veyra_requests_total[5m])

# Rate-limit rejections per second
rate(veyra_ratelimit_exceeded_total[5m])

# Proxy error rate
rate(veyra_proxy_errors_total[5m])

# Ejected destinations (alert when > 0)
veyra_destinations_ejected

# Healthy vs ejected
veyra_destinations_healthy
```

## Suggested alerts

- `rate(veyra_proxy_errors_total[5m]) > 0` sustained — upstream or gateway fault
- `veyra_destinations_ejected > 0` — passive health ejection active
- `rate(veyra_auth_failures_total[5m]) / rate(veyra_requests_total[5m]) > 0.1` — credential or policy issue

## Grafana

Import [grafana-veyra-golden-signals.json](grafana-veyra-golden-signals.json) as a starter dashboard.

See [observability](observability.md) and [health](health.md).
