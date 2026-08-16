#!/usr/bin/env bash
# stop-mcps.sh - stop everything (inverse of scripts/start-mcps.sh).
#
#   1. MCP workloads on the HOST via ToolHive: `thv rm` + `docker rm -f` +
#      runconfig/status cleanup for every workload (same as start-mcps.sh's
#      clean_state).
#   2. docker compose -f deploy/docker-compose.yaml down - the gateway
#      (stock agentgateway) + Keycloak + observability stack.
#
# Usage:  ./scripts/stop-mcps.sh [--ratelimit]
#         Pass --ratelimit when the stack was started with --ratelimit so
#         the Envoy ratelimit override is torn down too.
set -euo pipefail
cd "$(dirname "$0")/.."

COMPOSE_FILES="-f deploy/docker-compose.yaml"
for arg in "$@"; do
  case "$arg" in
    --ratelimit) COMPOSE_FILES="$COMPOSE_FILES -f deploy/docker-compose.ratelimit.yaml" ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

WORKLOADS=(mcp-everything mcp-time)

echo "==> [1/2] MCP workloads on the host (thv)"
for w in "${WORKLOADS[@]}"; do
  thv rm "$w" >/dev/null 2>&1 || true
  docker rm -f "$w" >/dev/null 2>&1 || true
done
rm -f \
  "$HOME/.local/state/toolhive/runconfigs/mcp-everything.json" \
  "$HOME/.local/state/toolhive/runconfigs/mcp-time.json" \
  "$HOME/.local/state/toolhive/statuses/mcp-everything.json" \
  "$HOME/.local/state/toolhive/statuses/mcp-time.json"
LOCAL_STATE="${LOCALAPPDATA:-}"
if [[ -n "$LOCAL_STATE" ]]; then
  rm -f \
    "$LOCAL_STATE/toolhive/runconfigs/mcp-everything.json" \
    "$LOCAL_STATE/toolhive/runconfigs/mcp-time.json" \
    "$LOCAL_STATE/toolhive/statuses/mcp-everything.json" \
    "$LOCAL_STATE/toolhive/statuses/mcp-time.json"
fi

echo "==> [2/2] Gateway + Keycloak + observability"
docker compose $COMPOSE_FILES down
echo "Stack stopped (gateway, keycloak, otel-collector, prometheus, tempo, loki, grafana, langfuse)."
echo "To also remove volumes (incl. gateway logs + keycloak realm/signing keys): docker compose -f deploy/docker-compose.yaml down -v"
