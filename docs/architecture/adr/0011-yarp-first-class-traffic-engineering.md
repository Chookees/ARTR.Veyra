# ADR-0011: YARP-first-class traffic engineering

## Status

Accepted

## Context

Operators need canary weights, health-based ejection, and load-balancing defaults without learning every YARP cluster schema detail. Core must remain free of YARP references (ADR-0006).

## Decision

Expose first-class Veyra options that map to YARP configuration via `VeyraTrafficProxyConfigFilter` (`IProxyConfigFilter` in Host):

| Veyra option | YARP mapping |
|--------------|--------------|
| `Canary.Splits[].Weights` | Destination `Metadata.Weight` per cluster |
| `Canary.Splits` validation | Weights must sum to **100** per split |
| `TrafficEngineering.OutlierDetection` | Passive health: `Enabled=true`, `Policy=TransportFailureRate`, `ReactivationPeriod=EjectionDurationSeconds` |
| Cluster `LoadBalancingPolicy` unset | Default **`PowerOfTwoChoices`** |
| `Health` (active/passive) | YARP `HealthCheck` on clusters (configured in ReverseProxy section) |

Core holds options and validation (`VeyraOptionsValidator`, `ReverseProxyRouteSecurityValidator`). Host applies the adapter filter at config load time. No YARP types in Core.

Destination health gauges (`veyra.destinations.healthy`, `veyra.destinations.ejected`) are published by `DestinationHealthMetricsPublisher` from YARP proxy state.

## Consequences

- Traffic features are configured in Veyra options plus standard YARP cluster/route JSON
- Advanced YARP tuning remains available via raw ReverseProxy config
- Filter changes require Host deployment; Core option schema can evolve independently
