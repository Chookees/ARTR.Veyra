#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Building ARTR Veyra demo (Release)..."
dotnet build ARTR.Veyra.sln -c Release

CONFIG="$ROOT/config/veyra.example.json"
BACKUP="$(mktemp)"
cp "$CONFIG" "$BACKUP"

dotnet run --project samples/ARTR.Veyra.Sample.UpstreamA -c Release --no-build --no-launch-profile &
PID_A=$!
dotnet run --project samples/ARTR.Veyra.Sample.UpstreamB -c Release --no-build --no-launch-profile &
PID_B=$!
sleep 2
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/ARTR.Veyra.Host -c Release --no-build --no-launch-profile -- --urls http://127.0.0.1:5080 &
PID_G=$!

cleanup() {
  cp "$BACKUP" "$CONFIG" || true
  rm -f "$BACKUP"
  kill $PID_G $PID_A $PID_B 2>/dev/null || true
}
trap cleanup EXIT

for i in $(seq 1 60); do
  if curl -sf http://127.0.0.1:5080/_veyra/health/live >/dev/null; then
    break
  fi
  sleep 0.5
done

echo ""
echo "ARTR Veyra demo — Envoy-inspired .NET gateway (no containers)"
echo "  Gateway: http://127.0.0.1:5080"
echo "==> Proxy"
curl -s http://127.0.0.1:5080/a/hello; echo
curl -s http://127.0.0.1:5080/b/hello; echo
echo "==> Canary"
for i in 1 2 3 4 5 6; do curl -s http://127.0.0.1:5080/canary/hello; echo; done
echo "==> Diagnostics"
curl -s http://127.0.0.1:5080/_veyra/diagnostics; echo
echo "Demo ready. Ctrl+C to stop."
wait $PID_G
