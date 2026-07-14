#!/usr/bin/env bash
set -euo pipefail

if command -v helm >/dev/null 2>&1; then
  echo "helm already installed"
  helm version
  exit 0
fi

tmp_script="$(mktemp)"
curl -fsSL -o "$tmp_script" https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3
chmod 700 "$tmp_script"
"$tmp_script"
rm -f "$tmp_script"

helm version