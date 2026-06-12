#!/usr/bin/env bash

set -euo pipefail

sample_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output=$(bash "$sample_root/scripts/validate-instructions.sh")
echo "$output"

if [[ "$output" != *"Validated 2 instruction file(s)"* ]]; then
  echo "Expected validator to report two instruction files" >&2
  exit 1
fi

echo "per-service-copilot-instructions sample passed"
