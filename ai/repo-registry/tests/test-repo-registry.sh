#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp_root="$(mktemp -d)"
workspace_root="$tmp_root/workspace"
mkdir -p "$workspace_root"

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

create_repo() {
  local name="$1"
  local path="$workspace_root/$name"
  mkdir -p "$path"
  git init -q "$path"
  git -C "$path" config user.email "sample@example.com"
  git -C "$path" config user.name "Sample User"
  printf "# %s\n" "$name" > "$path/README.md"
  git -C "$path" add README.md
  git -C "$path" commit -q -m "Initial commit"
}

create_repo "Shared.Contracts"
create_repo "Orders.Api"
create_repo "Billing.Worker"
create_repo "AdminPortal.Angular"
create_repo "Platform.Infrastructure"

printf "dirty\n" >> "$workspace_root/Shared.Contracts/README.md"

registry_path="$tmp_root/repos.json"
workspace_root_windows=$(cygpath -m "$workspace_root")
sed "s#\"path\": \"#\"path\": \"$workspace_root_windows/#g" "$sample_root/sample-data/repos.json" > "$registry_path"

describe_output=$(dotnet run --project "$sample_root/src/RepoRegistry.McpServer" -- --registry "$registry_path" describe)
echo "$describe_output"
if [[ "$describe_output" != *'"serverName": "repo-registry"'* || "$describe_output" != *'"list_repos"'* ]]; then
  echo "Expected server description and tool names" >&2
  exit 1
fi

list_output=$(dotnet run --project "$sample_root/src/RepoRegistry.McpServer" -- --registry "$registry_path" --tool list_repos)
echo "$list_output"
if [[ "$list_output" != *'"name": "Shared.Contracts"'* ]]; then
  echo "Expected Shared.Contracts in list-repos output" >&2
  exit 1
fi

status_output=$(dotnet run --project "$sample_root/src/RepoRegistry.McpServer" -- --registry "$registry_path" --tool get_workspace_status)
echo "$status_output"
if [[ "$status_output" != *'"name": "Shared.Contracts"'* || "$status_output" != *'"hasUncommitted": true'* ]]; then
  echo "Expected dirty Shared.Contracts repo in workspace-status output" >&2
  exit 1
fi

dependents_output=$(dotnet run --project "$sample_root/src/RepoRegistry.McpServer" -- --registry "$registry_path" --tool find_dependents Shared.Contracts)
echo "$dependents_output"
if [[ "$dependents_output" != *'"Orders.Api"'* || "$dependents_output" != *'"AdminPortal.Angular"'* ]]; then
  echo "Expected recursive dependents in find-dependents output" >&2
  exit 1
fi

echo "repo-registry sample passed"
