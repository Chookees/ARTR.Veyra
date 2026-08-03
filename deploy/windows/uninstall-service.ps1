param(
  [string] $ServiceName = 'ARTR Veyra'
)

$ErrorActionPreference = 'Stop'
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
  Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
  sc.exe delete $ServiceName | Out-Null
  Write-Host "Removed Windows Service '$ServiceName'."
} else {
  Write-Host "Service '$ServiceName' was not found."
}
