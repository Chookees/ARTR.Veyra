#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet build ARTR.Veyra.sln -c Release
dotnet run --project samples/ARTR.Veyra.Sample.UpstreamA -c Release --no-build &
PID_A=$!
dotnet run --project samples/ARTR.Veyra.Sample.UpstreamB -c Release --no-build &
PID_B=$!
sleep 2
dotnet run --project src/ARTR.Veyra.Host -c Release --no-build -- --urls http://127.0.0.1:5080 &
PID_G=$!
cleanup() { kill $PID_G $PID_A $PID_B 2>/dev/null || true; }
trap cleanup EXIT
echo "Gateway http://127.0.0.1:5080  Admin /_veyra/info"
wait $PID_G
