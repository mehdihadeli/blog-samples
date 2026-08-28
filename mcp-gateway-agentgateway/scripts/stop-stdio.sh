#!/usr/bin/env bash
# stop-stdio.sh — stop everything (inverse of scripts/start-stdio.sh).
#
#   kill -TERM the host gateway binary (deployments/config.stdio.yaml).
#   The Keycloak + observability stack is managed separately. The agentgateway binary and the pip
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
# Fallback: kill any agentgateway we started with deployments/config.stdio.yaml.
pkill -f "agentgateway -f deployments/config.stdio.yaml" >/dev/null 2>&1 || true

echo "Compose stack was not changed. Stop it separately with: docker compose -f deployments/docker-compose.stdio.yml down"
