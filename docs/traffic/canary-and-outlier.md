# Canary splits and outlier detection

## Canary weights

Enable canary traffic splitting under `ARTR:Veyra:Canary`:

```json
"Canary": {
  "Enabled": true,
  "Splits": [
    {
      "Name": "api-rollout",
      "ClusterId": "cluster-api",
      "Weights": {
        "stable": 90,
        "canary": 10
      }
    }
  ]
}
```

Rules (enforced by `ReverseProxyRouteSecurityValidator`):

- Each split references an existing `ReverseProxy:Clusters` entry
- `Weights` keys must match destination IDs in that cluster
- Weights must be **0–100** and **sum to 100**

`VeyraTrafficProxyConfigFilter` writes weights to destination `Metadata.Weight` at config load. YARP weighted load balancing distributes traffic accordingly.

## Header-match (sticky canary)

For deterministic routing (e.g. internal testers always hit canary), set **both** `MatchHeaderName` and `MatchHeaderValue` on the split **and** add a matching YARP route:

```json
"Splits": [
  {
    "Name": "api-canary-cohort",
    "ClusterId": "cluster-api",
    "MatchHeaderName": "X-Canary",
    "MatchHeaderValue": "true",
    "Weights": { "stable": 0, "canary": 100 }
  }
]
```

```json
"Routes": {
  "api-canary-header": {
    "ClusterId": "cluster-api",
    "Match": {
      "Path": "/api/{**catch-all}",
      "Headers": [
        { "Name": "X-Canary", "Values": ["true"], "Mode": "ExactHeader" }
      ]
    }
  },
  "api-default": {
    "ClusterId": "cluster-api",
    "Match": { "Path": "/api/{**catch-all}" }
  }
}
```

Veyra validates the split header pair; YARP `Match.Headers` performs the actual sticky routing. Use route ordering so the header-matched route is evaluated first.

## Outlier detection

`TrafficEngineering.OutlierDetection` maps to YARP **passive health**:

| Veyra option | Effect |
|--------------|--------|
| `Enabled` | Enables passive health on clusters |
| `EjectionDurationSeconds` | `Passive.ReactivationPeriod` — how long ejected destinations stay out |
| `ConsecutiveFailureEjectionThreshold` | Documented operator tuning; pairs with YARP active health `ConsecutiveFailures` policy when configured |

Ejected destinations increment `veyra.destinations.ejected`. Inspect live state via `GET /_veyra/diagnostics`.

## Circuit breaker semantics

Veyra does **not** ship a second circuit-breaker framework (e.g. Polly). Resilience is:

1. **Passive health** — transport-failure ejection and reactivation (outlier detection)
2. **Connection limits** — YARP forwarder `HttpClient` pool defaults and Kestrel limits

Configure active health checks on clusters for probe-based removal; passive health handles runtime failure bursts.

See [golden signals](../operations/golden-signals.md) and ADR-0011.
