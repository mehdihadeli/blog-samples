#!/usr/bin/env bash
# stop-mcpwrap.sh — stop everything (inverse of scripts/start-mcpwrap.sh).
#
#   1. MCP workloads on the HOST via the mcpwrap wrapper: SIGTERM the daemon
#      (its signal handler stops all containers and clears state), then
#      `mcpwrap down` as a belt-and-braces fallback, then remove any
#      orphaned mcpwrap-* containers.
#   2. docker compose -f docker-compose.mcpwrap.yml down — the gateway
#      (stock agentgateway) + Keycloak + observability stack.
#
# Usage:  ./scripts/stop-mcpwrap.sh
set -euo pipefail
cd "$(dirname "$0")/.."

MCPWRAP_BIN="mcpwrap/mcpwrap.exe"
MCPWRAP_PID="logs/mcpwrap.pid"

echo "==> [1/2] MCP workloads on the host (mcpwrap)"
if [[ -f "$MCPWRAP_PID" ]]; then
  kill -TERM "$(cat "$MCPWRAP_PID")" >/dev/null 2>&1 || true
  rm -f "$MCPWRAP_PID"
fi
if [[ -x "$MCPWRAP_BIN" ]]; then
  "$MCPWRAP_BIN" down >/dev/null 2>&1 || true
fi
docker ps -aq --filter "name=mcpwrap-" 2>/dev/null | xargs -r docker rm -f >/dev/null 2>&1 || true

echo "==> [2/2] Gateway + Keycloak + observability"
docker compose -f docker-compose.mcpwrap.yml down
echo "Stack stopped (gateway, keycloak, otel-collector, prometheus, tempo, loki, grafana, langfuse, phoenix)."
echo "To also remove volumes (incl. the memory graph + gateway logs): docker compose -f docker-compose.mcpwrap.yml down -v"
