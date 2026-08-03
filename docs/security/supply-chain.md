# Supply chain

ARTR Veyra follows practices to reduce dependency and build risk. **No third-party certification** (ISO, SOC, etc.) is claimed for this repository.

## Dependencies

- Central package management via `Directory.Packages.props`
- Lock files (`packages.lock.json`) per project; CI restores with `--locked-mode`
- `NuGetAudit` enabled at `low` severity; advisories fail the build when they surface as errors

## Automation

| Control | Mechanism |
|---------|-----------|
| Dependency review | `.github/workflows/dependency-review.yml` on pull requests |
| Code scanning | CodeQL workflow |
| Scheduled security | Scheduled workflow for audit and dependency checks |
| Coverage gate | CI fails below 90% line and branch coverage on production assemblies |

## Build reproducibility

- Pinned SDK in `global.json`
- No container images in the default build path (ADR-0002)
- Release workflow publishes framework-dependent deployments for declared RIDs

## Operator responsibilities

- Monitor GitHub security advisories for your fork
- Verify package signatures and source when mirroring feeds
- Maintain an inventory of deployed versions and known CVE responses

See [compliance EvidenceIndex](../compliance/EvidenceIndex.md).
