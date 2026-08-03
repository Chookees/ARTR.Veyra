# Contributing to ARTR Veyra

Thank you for contributing.

## Development prerequisites

- .NET 10 SDK
- Git
- PowerShell 7 or Bash

Containers are **not** used or required.

## Getting started

```bash
dotnet tool restore
dotnet restore ARTR.Veyra.sln --locked-mode
dotnet build ARTR.Veyra.sln --configuration Release
dotnet test ARTR.Veyra.sln --configuration Release --no-build
```

## Coding standards

- Root namespace: `ARTR.Veyra`
- Treat warnings as errors
- No TODOs, stubs, or `NotImplementedException` in production code
- Core must not reference YARP or ASP.NET hosting
- Prefer small, testable units with clear boundaries

## Pull requests

1. Fork and create a feature branch
2. Add or update tests
3. Ensure format, build, and tests pass
4. Fill out the pull request template

## Security

Report vulnerabilities via GitHub private vulnerability reporting. See [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.
