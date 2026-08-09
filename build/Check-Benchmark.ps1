#!/usr/bin/env pwsh
param(
  [string]$ResultsDir = 'artifacts/benchmarks',
  [switch]$SoftFail
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $ResultsDir)) {
  Write-Warning "No benchmark results at $ResultsDir"
  if ($SoftFail) { exit 0 }
  exit 1
}

$jsonFiles = Get-ChildItem -Path $ResultsDir -Recurse -Filter '*.json' -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match 'report|results|BenchmarkReport' -or $_.Length -gt 0 }

if (-not $jsonFiles) {
  Write-Warning "Benchmark JSON not found under $ResultsDir (soft gate)."
  if ($SoftFail) { exit 0 }
  exit 1
}

Write-Host "Found $($jsonFiles.Count) benchmark artifact(s). Soft gate: publish measured tables only; no invented numbers."
# Soft-fail mode: always succeed once artifacts exist. Hard thresholds can be added later per benchmark.
exit 0
