# Development

Use the scripts in `build/` for restore/build/test/publish.
Central Package Management is in `Directory.Packages.props`.
Package lock files must be updated with `dotnet restore` when dependencies change and committed.

Coverage target: ≥90% line and branch on production assemblies (see `coverlet.runsettings`).
