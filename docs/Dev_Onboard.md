# Developer onboarding

1. Install .NET 10 SDK and Git.
2. Clone the repository.
3. Run `dotnet tool restore` and `dotnet restore ARTR.Veyra.sln --locked-mode`.
4. Build and test in Release.
5. Read ADRs under `docs/architecture/adr/`.
6. Prefer TDD for Core/Infrastructure changes; use WebApplicationFactory for Host behavior.

Coding standards: nullable enabled, warnings as errors, English-only comments/docs, acyclic project references.
