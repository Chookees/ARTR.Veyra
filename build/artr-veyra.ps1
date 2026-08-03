#!/usr/bin/env pwsh
# CLI shim for ARTR Veyra (artr-veyra)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $root 'src/ARTR.Veyra.Host/ARTR.Veyra.Host.csproj'
dotnet run --project $hostProject --configuration Release -- @args
