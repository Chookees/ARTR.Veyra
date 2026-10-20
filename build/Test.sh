#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
results="$ROOT/artifacts/TestResults"
report_dir="$ROOT/artifacts/coverage-report"
rm -rf "$results" "$report_dir"
mkdir -p "$results"
# xunit.v3 4.x uses MTP v2; collect coverage via coverlet.MTP (+ tests/coverlet.testconfig.json).
dotnet test ARTR.Veyra.sln --configuration Release \
  --coverlet \
  --results-directory "$results"
dotnet tool run reportgenerator \
  "-reports:$results/**/coverage.cobertura*.xml" \
  "-targetdir:$report_dir" \
  "-reporttypes:TextSummary" \
  "-assemblyfilters:+ARTR.Veyra.Core;+ARTR.Veyra.Infrastructure;+ARTR.Veyra.Authentication;+ARTR.Veyra.Observability;+ARTR.Veyra.Host"
"$ROOT/build/check-coverage.sh" 90
