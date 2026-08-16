#!/usr/bin/env bash
# stop-vmcp.sh — stop everything (inverse of scripts/start-vmcp.sh).
#
#   1. `thv vmcp serve` on the HOST: kill the process from logs/vmcp.pid.
#   2. MCP workloads on the HOST via ToolHive: `thv rm` + `docker rm -f` +
#      runconfig/status cleanup for every workload (same as start-vmcp.sh's
#      clean_state), then remove the `mcp-vmcp` group.
#   3. docker compose -f deployments/docker-compose.vmcp.yml down — Keycloak.
#
# Usage:  ./scripts/stop-vmcp.sh
set -euo pipefail
cd "$(dirname "$0")/.."

WORKLOADS=(mcp-fetch mcp-memory mcp-sequentialthinking mcp-everything)
GROUP=mcp-vmcp

echo "==> [1/3] Stop the vMCP server (thv vmcp serve)"
if [[ -f logs/vmcp.pid ]]; then
  kill "$(cat logs/vmcp.pid)" >/dev/null 2>&1 || true
  rm -f logs/vmcp.pid
else
  echo "    no logs/vmcp.pid found (vMCP may not be running)"
fi

echo "==> [2/3] MCP workloads on the host (thv)"
for w in "${WORKLOADS[@]}"; do
  thv rm "$w" >/dev/null 2>&1 || true
  docker rm -f "$w" >/dev/null 2>&1 || true
done
rm -f \
  "$HOME/.local/state/toolhive/runconfigs/mcp-fetch.json" \
  "$HOME/.local/state/toolhive/runconfigs/mcp-memory.json" \
  "$HOME/.local/state/toolhive/runconfigs/mcp-sequentialthinking.json" \
  "$HOME/.local/state/toolhive/runconfigs/mcp-everything.json" \
  "$HOME/.local/state/toolhive/statuses/mcp-fetch.json" \
  "$HOME/.local/state/toolhive/statuses/mcp-memory.json" \
  "$HOME/.local/state/toolhive/statuses/mcp-sequentialthinking.json" \
  "$HOME/.local/state/toolhive/statuses/mcp-everything.json"
LOCAL_STATE="${LOCALAPPDATA:-}"
if [[ -n "$LOCAL_STATE" ]]; then
  rm -f \
    "$LOCAL_STATE/toolhive/runconfigs/mcp-fetch.json" \
    "$LOCAL_STATE/toolhive/runconfigs/mcp-memory.json" \
    "$LOCAL_STATE/toolhive/runconfigs/mcp-sequentialthinking.json" \
    "$LOCAL_STATE/toolhive/runconfigs/mcp-everything.json" \
    "$LOCAL_STATE/toolhive/statuses/mcp-fetch.json" \
    "$LOCAL_STATE/toolhive/statuses/mcp-memory.json" \
    "$LOCAL_STATE/toolhive/statuses/mcp-sequentialthinking.json" \
    "$LOCAL_STATE/toolhive/statuses/mcp-everything.json"
fi
thv group rm "$GROUP" >/dev/null 2>&1 || true

echo "==> [3/3] Keycloak"
docker compose -f deployments/docker-compose.vmcp.yml down
echo "Stack stopped (vMCP, thv workloads, keycloak)."
echo "To also remove the keycloak data volume (realm + signing keys): docker compose -f deployments/docker-compose.vmcp.yml down -v"
