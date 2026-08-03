# ADR-0005: Prometheus exporter beta pin

## Status

Accepted

## Context

`OpenTelemetry.Exporter.Prometheus.AspNetCore` has no stable NuGet release matching OpenTelemetry 1.17; only beta builds are published.

## Decision

Pin `OpenTelemetry.Exporter.Prometheus.AspNetCore` to `1.17.0-beta.1` and enable it only when `ARTR:Veyra:Observability:Prometheus:Enabled` is true.

## Consequences

- Prometheus scraping works when opted in.
- Operators accept beta package risk; OTLP remains the stable export path.
