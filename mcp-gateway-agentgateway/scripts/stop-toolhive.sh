#!/usr/bin/env bash
# stop-toolhive.sh — stop everything (inverse of scripts/start-toolhive.sh).
#
#   1. MCP workloads on the HOST via ToolHive: `thv rm` + `docker rm -f` +
#      runconfig/status cleanup for every workload (same as start-toolhive.sh's
#      clean_state).
#   2. docker compose -f docker-compose.toolhive.yml down — the gateway
#      (stock agentgateway) + Keycloak + observability stack.
#
# Usage:  ./scripts/stop-toolhive.sh
set -euo pipefail
cd "$(dirname "$0")/.."

WORKLOADS=(mcp-fetch mcp-memory mcp-sequentialthinking mcp-everything)

echo "==> [1/2] MCP workloads on the host (thv)"
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

echo "==> [2/2] Gateway + Keycloak + observability"
docker compose -f docker-compose.toolhive.yml down
echo "Stack stopped (gateway, keycloak, otel-collector, prometheus, tempo, loki, grafana, langfuse, phoenix)."
echo "To also remove volumes (incl. the memory graph + gateway logs): docker compose -f docker-compose.toolhive.yml down -v"
