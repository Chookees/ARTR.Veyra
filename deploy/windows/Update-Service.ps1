param(
  [Parameter(Mandatory = $true)]
  [string] $BinaryDirectory,
  [string] $InstallDirectory,
  [string] $ServiceName = 'ARTR Veyra'
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $BinaryDirectory 'ARTR.Veyra.Host.exe'
if (-not (Test-Path $exe)) {
  throw "Host executable not found at $exe"
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
  throw "Service '$ServiceName' is not installed. Run Install-Service.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
  $InstallDirectory = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").PathName.Trim('"')
  $InstallDirectory = Split-Path -Parent $InstallDirectory
}

Stop-Service -Name $ServiceName -Force
Copy-Item -Path (Join-Path $BinaryDirectory '*') -Destination $InstallDirectory -Recurse -Force
Start-Service -Name $ServiceName
Write-Host "Updated binaries in '$InstallDirectory' and restarted '$ServiceName'."
