param(
  [Parameter(Mandatory = $true)]
  [string] $BinaryDirectory,
  [string] $ServiceName = 'ARTR Veyra'
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $BinaryDirectory 'ARTR.Veyra.Host.exe'
if (-not (Test-Path $exe)) {
  throw "Host executable not found at $exe"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
  throw "Service '$ServiceName' already exists. Use Update-Service.ps1 or Uninstall-Service.ps1 first."
}

New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName $ServiceName -StartupType Automatic
Start-Service -Name $ServiceName
Write-Host "Installed and started Windows Service '$ServiceName'."
