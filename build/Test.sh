#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
results="$ROOT/artifacts/TestResults"
report_dir="$ROOT/artifacts/coverage-report"
rm -rf "$results" "$report_dir"
mkdir -p "$results"
dotnet test ARTR.Veyra.sln --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory "$results" \
  --settings "$ROOT/coverlet.runsettings"
dotnet tool run reportgenerator \
  "-reports:$results/**/coverage.cobertura.xml" \
  "-targetdir:$report_dir" \
  "-reporttypes:TextSummary" \
  "-assemblyfilters:+ARTR.Veyra.Core;+ARTR.Veyra.Infrastructure;+ARTR.Veyra.Authentication;+ARTR.Veyra.Observability;+ARTR.Veyra.Host"
"$ROOT/build/check-coverage.sh" 90
