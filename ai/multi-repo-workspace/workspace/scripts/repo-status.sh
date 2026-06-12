#!/usr/bin/env bash

set -euo pipefail

workspace_root="${WORKSPACE_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
repos=(
  "Company.Billing.Contracts"
  "Company.Billing.Api"
  "Company.Jobs.Worker"
  "Company.Admin.Angular"
  "Company.Infrastructure"
)

for repo in "${repos[@]}"; do
  path="$workspace_root/$repo"

  if [[ ! -d "$path" ]]; then
    echo "[$repo] missing"
    continue
  fi

  branch=$(git -C "$path" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")
  dirty=$(git -C "$path" status --short 2>/dev/null | wc -l | tr -d ' ')

  echo "[$repo] branch=$branch dirty=$dirty"
done
