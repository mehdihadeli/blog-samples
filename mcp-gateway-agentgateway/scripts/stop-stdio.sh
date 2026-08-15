#!/usr/bin/env bash
# stop-stdio.sh — stop everything (inverse of scripts/start-stdio.sh).
#
#   kill -TERM the host gateway binary (config.stdio.yaml) + `compose down`
#   on the Keycloak + observability stack (docker-compose.stdio.yml — no
#   gateway service in this variant). The agentgateway binary and the pip
#   packages stay installed on the host for the next start.
#
# Usage:  ./scripts/stop-stdio.sh
set -euo pipefail
cd "$(dirname "$0")/.."

GATEWAY_PID="logs/agentgateway-stdio.pid"

echo "==> [1/2] Stop the host gateway binary"
if [[ -f "$GATEWAY_PID" ]]; then
  kill -TERM "$(cat "$GATEWAY_PID")" >/dev/null 2>&1 || true
  rm -f "$GATEWAY_PID"
fi
# Fallback: kill any agentgateway we started with config.stdio.yaml.
pkill -f "agentgateway -f config.stdio.yaml" >/dev/null 2>&1 || true

echo "==> [2/2] Keycloak + observability stack"
docker compose -f docker-compose.stdio.yml down
echo "Stack stopped (keycloak, otel-collector, prometheus, tempo, loki, grafana, langfuse, phoenix)."
echo "To also remove volumes (incl. keycloak data): docker compose -f docker-compose.stdio.yml down -v"
