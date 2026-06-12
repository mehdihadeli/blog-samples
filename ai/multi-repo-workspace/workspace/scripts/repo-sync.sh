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
    echo "Skipping missing repo: $repo" >&2
    continue
  fi

  if ! git -C "$path" remote get-url origin >/dev/null 2>&1; then
    echo "[$repo] no origin configured"
    continue
  fi

  echo "Fetching $repo"
  git -C "$path" fetch origin
done
