#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root 'artifacts/publish'
$rids = @('win-x64','win-arm64','linux-x64','linux-arm64')
foreach ($rid in $rids) {
  $fdd = Join-Path $out "fdd/$rid"
  $scd = Join-Path $out "scd/$rid"
  New-Item -ItemType Directory -Force -Path $fdd,$scd | Out-Null
  dotnet publish src/ARTR.Veyra.Host/ARTR.Veyra.Host.csproj -c Release -r $rid --self-contained false -o $fdd /p:PublishSingleFile=false
  dotnet publish src/ARTR.Veyra.Host/ARTR.Veyra.Host.csproj -c Release -r $rid --self-contained true -o $scd /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
}
Write-Host "Published to $out"
