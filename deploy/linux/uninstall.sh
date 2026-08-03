#!/usr/bin/env bash
set -euo pipefail
if systemctl is-enabled artr-veyra &>/dev/null; then
  sudo systemctl disable --now artr-veyra
fi
if [[ -f /etc/systemd/system/artr-veyra.service ]]; then
  sudo rm -f /etc/systemd/system/artr-veyra.service
  sudo systemctl daemon-reload
fi
echo "ARTR Veyra systemd unit removed. Application files under /opt/artr-veyra are not deleted."
