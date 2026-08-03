param(
    [double] $MinimumPercent = 90,
    [string] $SummaryPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $root = Split-Path -Parent $PSScriptRoot
    $SummaryPath = Join-Path $root 'artifacts/coverage-report/Summary.txt'
}

if (-not (Test-Path $SummaryPath)) {
    throw "Coverage summary not found at $SummaryPath. Run tests with coverage and reportgenerator first."
}

$summary = Get-Content $SummaryPath
$failed = $false

foreach ($metric in @('Line', 'Branch')) {
    $line = $summary | Where-Object { $_ -match "^\s+$metric coverage:\s+([\d.]+)%" } | Select-Object -First 1
    if (-not $line) {
        throw "Could not parse $metric coverage from $SummaryPath"
    }

    $percent = [double]$Matches[1]
    Write-Host "$metric coverage: $percent% (minimum $MinimumPercent%)"
    if ($percent -lt $MinimumPercent) {
        Write-Error "$metric coverage $percent% is below minimum $MinimumPercent%"
        $failed = $true
    }
}

if ($failed) {
    exit 1
}

Write-Host "Coverage gate passed."
