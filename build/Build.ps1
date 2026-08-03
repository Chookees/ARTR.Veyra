#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
dotnet tool restore
dotnet restore ARTR.Veyra.sln --locked-mode
dotnet build ARTR.Veyra.sln --configuration Release --no-restore
