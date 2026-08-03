#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT="$ROOT/artifacts/publish"
for rid in win-x64 win-arm64 linux-x64 linux-arm64; do
  mkdir -p "$OUT/fdd/$rid" "$OUT/scd/$rid"
  dotnet publish src/ARTR.Veyra.Host/ARTR.Veyra.Host.csproj -c Release -r "$rid" --self-contained false -o "$OUT/fdd/$rid"
  dotnet publish src/ARTR.Veyra.Host/ARTR.Veyra.Host.csproj -c Release -r "$rid" --self-contained true -o "$OUT/scd/$rid" /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
done
echo "Published to $OUT"
