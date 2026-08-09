#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "==> Building ARTR Veyra demo (Release)..."
dotnet build ARTR.Veyra.sln -c Release | Out-Host

$configPath = Join-Path $root 'config/veyra.example.json'
$canaryBackup = Get-Content $configPath -Raw

# Make rate-limit demo deterministic without permanently changing the example.
$demoJson = $canaryBackup | ConvertFrom-Json
$demoJson.ARTR.Veyra.RateLimiting.Policies[0].PermitLimit = 5
($demoJson | ConvertTo-Json -Depth 100) | Set-Content -Path $configPath -Encoding utf8

$upstreamA = Start-Process dotnet -ArgumentList @('run','--project','samples/ARTR.Veyra.Sample.UpstreamA','-c','Release','--no-build','--no-launch-profile') -PassThru -WindowStyle Hidden
$upstreamB = Start-Process dotnet -ArgumentList @('run','--project','samples/ARTR.Veyra.Sample.UpstreamB','-c','Release','--no-build','--no-launch-profile') -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$gateway = Start-Process dotnet -ArgumentList @(
  'run','--project','src/ARTR.Veyra.Host','-c','Release','--no-build','--no-launch-profile',
  '--','--urls','http://127.0.0.1:5080'
) -PassThru -NoNewWindow

function Wait-Http([string]$Url, [int]$Seconds = 30) {
  $deadline = (Get-Date).AddSeconds($Seconds)
  while ((Get-Date) -lt $deadline) {
    try {
      $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
      if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) { return }
    } catch { }
    Start-Sleep -Milliseconds 500
  }
  throw "Timed out waiting for $Url"
}

try {
  Wait-Http 'http://127.0.0.1:5080/_veyra/health/live'

  Write-Host ""
  Write-Host "ARTR Veyra demo — Envoy-inspired .NET gateway (no containers)"
  Write-Host "  Gateway:     http://127.0.0.1:5080"
  Write-Host "  Upstream A:  http://127.0.0.1:5101"
  Write-Host "  Upstream B:  http://127.0.0.1:5102"
  Write-Host ""

  Write-Host "==> Proxy /a and /b"
  (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/a/hello' -UseBasicParsing).Content | Write-Host
  (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/b/hello' -UseBasicParsing).Content | Write-Host

  Write-Host "==> Canary split /canary (weights 90/10 via ARTR:Veyra:Canary)"
  1..8 | ForEach-Object {
    $body = (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/canary/hello' -UseBasicParsing).Content
    Write-Host "  hit $_ : $body"
  }

  Write-Host "==> Per-route rate limit on /b (policy 'strict', demo PermitLimit=5) — expect 429"
  $limited = $false
  for ($i = 0; $i -lt 20; $i++) {
    try {
      Invoke-WebRequest -Uri 'http://127.0.0.1:5080/b/rl' -UseBasicParsing | Out-Null
    } catch {
      $code = $_.Exception.Response.StatusCode.value__
      if ($code -eq 429) {
        Write-Host "  Got HTTP 429 after $i requests (IRateLimiterStore / route metadata)."
        $limited = $true
        break
      }
    }
  }
  if (-not $limited) { Write-Host "  (429 not observed — check RateLimiting.Enabled and Metadata.RateLimitPolicy.)" }

  Write-Host "==> Admin / diagnostics / health"
  (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/_veyra/info' -UseBasicParsing).Content | Write-Host
  (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/_veyra/diagnostics' -UseBasicParsing).Content | Write-Host
  (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/_veyra/health/ready' -UseBasicParsing).StatusCode | ForEach-Object { Write-Host "  ready: $_" }

  Write-Host "==> Canary flip 90/10 -> 50/50 via config reload"
  $json = Get-Content $configPath -Raw | ConvertFrom-Json
  $split = $json.ARTR.Veyra.Canary.Splits[0]
  $split.Weights.stable = 50
  $split.Weights.canary = 50
  ($json | ConvertTo-Json -Depth 100) | Set-Content -Path $configPath -Encoding utf8
  Start-Sleep -Seconds 3
  Write-Host "  After reload (weights 50/50):"
  1..6 | ForEach-Object {
    $body = (Invoke-WebRequest -Uri 'http://127.0.0.1:5080/canary/hello' -UseBasicParsing).Content
    Write-Host "  hit $_ : $body"
  }

  Write-Host ""
  Write-Host "Demo walkthrough complete. Gateway still running — Press Ctrl+C to stop."
  Wait-Process -Id $gateway.Id
}
finally {
  Set-Content -Path $configPath -Value $canaryBackup -Encoding utf8
  Stop-Process -Id $gateway.Id -Force -ErrorAction SilentlyContinue
  Stop-Process -Id $upstreamA.Id -Force -ErrorAction SilentlyContinue
  Stop-Process -Id $upstreamB.Id -Force -ErrorAction SilentlyContinue
}
