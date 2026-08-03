# AI onboarding — ARTR Veyra

## Product

Self-hosted L7 API gateway. Brand: **ARTR Veyra**. Namespace: `ARTR.Veyra`. Config: `ARTR:Veyra`. Admin: `/_veyra`. No containers.

## Architecture

- `ARTR.Veyra.Core` — options/validation/abstractions (no YARP)
- `ARTR.Veyra.Infrastructure` — secrets, memory rate store, transform validation
- `ARTR.Veyra.Authentication` — JWT + API key
- `ARTR.Veyra.Observability` — OpenTelemetry + correlation
- `ARTR.Veyra.Host` — composition root + YARP

## Constraints agents must respect

- TreatWarningsAsErrors, NuGet audit, locked restore
- No Docker/Testcontainers/Redis for default path
- No TODOs/stubs/NotImplementedException
- Do not invent security emails; use GitHub private vulnerability reporting

## Verify

```bash
dotnet restore ARTR.Veyra.sln --locked-mode
dotnet build ARTR.Veyra.sln -c Release --no-restore
dotnet test ARTR.Veyra.sln -c Release --no-build
```
