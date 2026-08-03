#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$base = if ($env:VEYRA_URL) { $env:VEYRA_URL.TrimEnd('/') } else { 'http://127.0.0.1:5080' }
$endpoints = @(
  '/_veyra/health/live',
  '/_veyra/health/ready',
  '/_veyra/health/startup',
  '/_veyra/info'
)
foreach ($path in $endpoints) {
  $url = "$base$path"
  Write-Host "GET $url"
  $response = Invoke-WebRequest -Uri $url -UseBasicParsing
  if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
    throw "Smoke test failed for $url with status $($response.StatusCode)"
  }
}
Write-Host 'Smoke test passed.'
