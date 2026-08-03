#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet tool restore
dotnet restore ARTR.Veyra.sln --locked-mode
dotnet build ARTR.Veyra.sln --configuration Release --no-restore
