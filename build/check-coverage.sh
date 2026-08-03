#!/usr/bin/env bash
set -euo pipefail

MINIMUM="${1:-90}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SUMMARY="${ROOT}/artifacts/coverage-report/Summary.txt"

if [[ ! -f "$SUMMARY" ]]; then
  echo "Coverage summary not found at $SUMMARY. Run tests with coverage and reportgenerator first." >&2
  exit 1
fi

failed=0
for metric in Line Branch; do
  percent="$(grep -E "^\s+${metric} coverage:" "$SUMMARY" | head -n1 | sed -E 's/.*: ([0-9.]+)%.*/\1/')"
  if [[ -z "$percent" ]]; then
    echo "Could not parse ${metric} coverage from $SUMMARY" >&2
    exit 1
  fi
  echo "${metric} coverage: ${percent}% (minimum ${MINIMUM}%)"
  if awk -v p="$percent" -v m="$MINIMUM" 'BEGIN { exit !(p < m) }'; then
    echo "${metric} coverage ${percent}% is below minimum ${MINIMUM}%" >&2
    failed=1
  fi
done

if [[ "$failed" -ne 0 ]]; then
  exit 1
fi

echo "Coverage gate passed."
