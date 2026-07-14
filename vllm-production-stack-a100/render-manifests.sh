#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-$ROOT_DIR/generated-manifests}"
RELEASE_NAME="${RELEASE_NAME:-vllm}"
CHART_DIR="$ROOT_DIR/helm"

mkdir -p "$OUTPUT_DIR"

render() {
  local output_file="$1"
  shift

  echo "Rendering $output_file"
  helm template "$RELEASE_NAME" "$CHART_DIR" "$@" > "$OUTPUT_DIR/$output_file"
}

render baseline.yaml -f "$ROOT_DIR/values.yaml"
render observability.yaml -f "$ROOT_DIR/values.yaml" -f "$ROOT_DIR/values.observability.yaml"

echo "Rendered manifests into $OUTPUT_DIR"