#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$upstreamA = Start-Process dotnet -ArgumentList @('run','--project','samples/ARTR.Veyra.Sample.UpstreamA','-c','Release','--no-build') -PassThru -WindowStyle Hidden
$upstreamB = Start-Process dotnet -ArgumentList @('run','--project','samples/ARTR.Veyra.Sample.UpstreamB','-c','Release','--no-build') -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
$gateway = Start-Process dotnet -ArgumentList @('run','--project','src/ARTR.Veyra.Host','-c','Release','--no-build','--','--urls','http://127.0.0.1:5080') -PassThru -NoNewWindow

Write-Host "ARTR Veyra demo running:"
Write-Host "  Gateway: http://127.0.0.1:5080"
Write-Host "  Upstream A: http://127.0.0.1:5101"
Write-Host "  Upstream B: http://127.0.0.1:5102"
Write-Host "  Admin: http://127.0.0.1:5080/_veyra/info"
Write-Host "Press Ctrl+C to stop."

try {
  Wait-Process -Id $gateway.Id
} finally {
  Stop-Process -Id $gateway.Id -Force -ErrorAction SilentlyContinue
  Stop-Process -Id $upstreamA.Id -Force -ErrorAction SilentlyContinue
  Stop-Process -Id $upstreamB.Id -Force -ErrorAction SilentlyContinue
}
