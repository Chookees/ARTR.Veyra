# ADR-0002: No containers for build, test, or run

## Status

Accepted

## Context

The product must be usable with only the .NET 10 SDK, Git, and OS tooling.

## Decision

Do not ship Dockerfiles, Compose files, Kubernetes manifests, Helm charts, Testcontainers, or container-based CI jobs. Prefer native `dotnet publish` FDD/SCD artifacts and OS service managers.

## Consequences

- CI and local workflows stay simple and portable.
- Operators bring their own process supervisor or edge TLS terminator if needed.
- Sample reverse-proxy configs may exist as plain files for Caddy/nginx without requiring those tools for tests.
