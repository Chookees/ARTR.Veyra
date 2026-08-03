#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$results = Join-Path $root 'artifacts/TestResults'
$reportDir = Join-Path $root 'artifacts/coverage-report'

# Leftover testhost processes can emit zero-hit Cobertura files and poison the merge.
Get-Process -Name 'testhost' -ErrorAction SilentlyContinue |
  Stop-Process -Force -ErrorAction SilentlyContinue

if (Test-Path $results) {
  Remove-Item -Recurse -Force $results
}
if (Test-Path $reportDir) {
  Remove-Item -Recurse -Force $reportDir
}
New-Item -ItemType Directory -Force -Path $results | Out-Null

dotnet test ARTR.Veyra.sln --configuration Release `
  --collect:"XPlat Code Coverage" `
  --results-directory $results `
  --settings (Join-Path $root 'coverlet.runsettings')
if ($LASTEXITCODE -ne 0) {
  throw "dotnet test failed with exit code $LASTEXITCODE"
}

$cobertura = @(Get-ChildItem -Recurse -Path $results -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue)
if ($cobertura.Count -lt 1) {
  throw "No coverage.cobertura.xml files found under $results"
}

dotnet tool run reportgenerator `
  "-reports:$results/**/coverage.cobertura.xml" `
  "-targetdir:$reportDir" `
  "-reporttypes:TextSummary" `
  "-assemblyfilters:+ARTR.Veyra.Core;+ARTR.Veyra.Infrastructure;+ARTR.Veyra.Authentication;+ARTR.Veyra.Observability;+ARTR.Veyra.Host"
& (Join-Path $PSScriptRoot 'Check-Coverage.ps1') -SummaryPath (Join-Path $reportDir 'Summary.txt')
