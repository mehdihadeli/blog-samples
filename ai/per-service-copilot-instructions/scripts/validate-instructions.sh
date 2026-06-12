#!/usr/bin/env bash

set -euo pipefail

repo_root="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/repos}"
required_sections=("## Architecture" "## Critical Rules" "## Tests")
found_files=0

while IFS= read -r file; do
  found_files=$((found_files + 1))
  echo "Checking $file"
  for section in "${required_sections[@]}"; do
    if ! grep -Fq "$section" "$file"; then
      echo "Missing section '$section' in $file" >&2
      exit 1
    fi
  done
done < <(find "$repo_root" \( -name AGENTS.md -o -path '*/.github/copilot-instructions.md' \) -type f | sort)

if [[ "$found_files" -eq 0 ]]; then
  echo "No instruction files found in $repo_root" >&2
  exit 1
fi

echo "Validated $found_files instruction file(s)"
