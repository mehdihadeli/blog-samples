#!/usr/bin/env bash
# stop-mcps.sh - stop the external MCP server managed by ToolHive.
#
#   MCP workloads on the HOST via ToolHive: `thv rm` + `docker rm -f` +
#      runconfig/status cleanup for every workload (same as start-mcps.sh's
#      clean_state).
#
# Usage:  ./scripts/stop-mcps.sh
set -euo pipefail
cd "$(dirname "$0")/.."

WORKLOADS=(mcp-everything mcp-sequentialthinking)

echo "==> MCP workloads on the host (thv)"
for w in "${WORKLOADS[@]}"; do
  thv rm "$w" >/dev/null 2>&1 || true
  docker rm -f "$w" >/dev/null 2>&1 || true
done
for w in "${WORKLOADS[@]}"; do
  rm -f \
    "$HOME/.local/state/toolhive/runconfigs/$w.json" \
    "$HOME/.local/state/toolhive/statuses/$w.json"
done
LOCAL_STATE="${LOCALAPPDATA:-}"
if [[ -n "$LOCAL_STATE" ]]; then
  for w in "${WORKLOADS[@]}"; do
    rm -f \
      "$LOCAL_STATE/toolhive/runconfigs/$w.json" \
      "$LOCAL_STATE/toolhive/statuses/$w.json"
  done
fi

echo "MCP proxy stopped. Docker Compose stack was left unchanged."
