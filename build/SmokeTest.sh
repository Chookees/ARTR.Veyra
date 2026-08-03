#!/usr/bin/env bash
set -euo pipefail
BASE="${VEYRA_URL:-http://127.0.0.1:5080}"
for path in /_veyra/health/live /_veyra/health/ready /_veyra/health/startup /_veyra/info; do
  echo "GET $BASE$path"
  curl -fsS "$BASE$path" >/dev/null
done
echo "Smoke test passed."
