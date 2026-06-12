#!/usr/bin/env bash

set -euo pipefail

workspace_root="${WORKSPACE_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"
repos=(
  "Company.Billing.Contracts"
  "Company.Billing.Api"
  "Company.Jobs.Worker"
  "Company.Admin.Angular"
  "Company.Infrastructure"
)

if [[ ! -d "$workspace_root" ]]; then
  echo "Workspace not found: $workspace_root" >&2
  exit 1
fi

missing=0
for repo in "${repos[@]}"; do
  repo_path="$workspace_root/$repo"
  if [[ ! -d "$repo_path" ]]; then
    echo "Missing repo: $repo_path" >&2
    missing=1
  fi
done

if [[ "$missing" -ne 0 ]]; then
  exit 1
fi

cd "$workspace_root"

if [[ "${1:-}" == "--dry-run" ]]; then
  echo "Workspace ready: $workspace_root"
  echo "Would run: gh copilot"
  exit 0
fi

if command -v gh >/dev/null 2>&1; then
  gh copilot
else
  echo "GitHub CLI is not installed. Run with --dry-run to validate the workspace layout." >&2
  exit 1
fi
