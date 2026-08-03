#!/usr/bin/env bash
set -euo pipefail
PREFIX="${1:-/opt/artr-veyra}"
UNIT_SRC="$(cd "$(dirname "$0")" && pwd)/systemd/artr-veyra.service"
install -d "$PREFIX"
echo "Copy published Host binaries into $PREFIX, then:"
echo "  sudo cp $UNIT_SRC /etc/systemd/system/artr-veyra.service"
echo "  sudo systemctl daemon-reload"
echo "  sudo systemctl enable --now artr-veyra"
