#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp_root="$(mktemp -d)"
workspace_root="$tmp_root/platform-workspace"
mkdir -p "$workspace_root"

repos=(
  "Company.Billing.Contracts"
  "Company.Billing.Api"
  "Company.Jobs.Worker"
  "Company.Admin.Angular"
  "Company.Infrastructure"
)

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

for repo in "${repos[@]}"; do
  path="$workspace_root/$repo"
  mkdir -p "$path"
  git init -q "$path"
  git -C "$path" config user.email "sample@example.com"
  git -C "$path" config user.name "Sample User"
  printf "# %s\n" "$repo" > "$path/README.md"
  git -C "$path" add README.md
  git -C "$path" commit -q -m "Initial commit"
done

printf "dirty\n" >> "$workspace_root/Company.Billing.Api/README.md"

status_output=$(WORKSPACE_ROOT="$workspace_root" bash "$sample_root/workspace/scripts/repo-status.sh")
echo "$status_output"

if [[ "$status_output" != *"[Company.Billing.Api] branch=master dirty=1"* && "$status_output" != *"[Company.Billing.Api] branch=main dirty=1"* ]]; then
  echo "Expected dirty API repo in status output" >&2
  exit 1
fi

start_output=$(WORKSPACE_ROOT="$workspace_root" bash "$sample_root/workspace/start-copilot.sh" --dry-run)
echo "$start_output"

if [[ "$start_output" != *"Would run: gh copilot"* ]]; then
  echo "Expected dry-run startup output" >&2
  exit 1
fi

sync_output=$(WORKSPACE_ROOT="$workspace_root" bash "$sample_root/workspace/scripts/repo-sync.sh")
echo "$sync_output"

if [[ "$sync_output" != *"[Company.Billing.Contracts] no origin configured"* ]]; then
  echo "Expected repo-sync to report missing remotes" >&2
  exit 1
fi

echo "multi-repo-workspace sample passed"
