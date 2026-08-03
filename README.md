# ARTR Veyra

**A secure, scalable, and observable open-source API gateway for modern distributed systems.**

ARTR Veyra is a self-hosted Layer-7 API gateway built on .NET 10 and YARP. It provides authentication, rate limiting, observability, and admin endpoints without requiring containers or external databases.

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build and test

```bash
dotnet restore ARTR.Veyra.sln
dotnet build ARTR.Veyra.sln -c Release --no-restore
dotnet test ARTR.Veyra.sln -c Release --no-build
```

Or use the build scripts:

```powershell
# Windows
.\build\Build.ps1
.\build\Test.ps1
```

```bash
# Linux / macOS
./build/Build.sh
./build/Test.sh
```

### Run the demo

```powershell
.\build\RunDemo.ps1
```

The gateway listens on `http://127.0.0.1:5080`. Sample upstreams run on ports 5101 and 5102.

Try:

```bash
curl http://127.0.0.1:5080/a/hello
curl http://127.0.0.1:5080/_veyra/info
curl http://127.0.0.1:5080/_veyra/health/live
```

### Configuration

- Config section: `ARTR:Veyra`
- Environment prefix: `ARTR_VEYRA_`
- Admin base path: `/_veyra`
- Example config: `config/veyra.example.json`

## Acceptance commands

```bash
dotnet tool restore
dotnet restore ARTR.Veyra.sln --locked-mode
dotnet format ARTR.Veyra.sln --verify-no-changes
dotnet build ARTR.Veyra.sln -c Release --no-restore
dotnet test ARTR.Veyra.sln -c Release --no-build --settings coverlet.runsettings
```

## Documentation

| Topic | Location |
|-------|----------|
| Usage guide | [docs/HowToUse.md](docs/HowToUse.md) |
| Developer onboarding | [docs/Dev_Onboard.md](docs/Dev_Onboard.md) |
| AI agent onboarding | [docs/AI_Onboard.md](docs/AI_Onboard.md) |
| Architecture | [docs/architecture/overview.md](docs/architecture/overview.md) |
| Configuration | [docs/configuration/README.md](docs/configuration/README.md) |
| Development | [docs/development/README.md](docs/development/README.md) |
| Operations | [docs/operations/README.md](docs/operations/README.md) |
| Security | [docs/security/README.md](docs/security/README.md) |

## License

Apache-2.0
