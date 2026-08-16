#!/usr/bin/env bash
# start-mcpwrap.sh — start the agentgateway MCP gateway sample with the
# mcpwrap runtime (a Go CLI that runs stdio MCP server containers and
# bridges them to Streamable HTTP).
#
# Three steps, all in one script:
#   1. Build the mcpwrap wrapper (Go) if needed, then start the MCP workloads
#      DETACHED on the HOST — memory :19101, fetch :19102,
#      sequentialthinking :19103 (`mcpwrap up -f mcpwrap/mcpwrap.json`).
#   2. Loki docker log driver plugin (idempotent) — the gateway container's
#      stdout streams to Loki through it (logging.driver=loki in compose).
#   3. docker compose -f deployments/docker-compose.mcpwrap.yml up -d — the gateway
#      (STOCK agentgateway image, no docker.sock) + Keycloak + observability
#      (otel-collector, prometheus, tempo, loki, grafana, langfuse, phoenix).
#
# Usage:
#   ./scripts/start-mcpwrap.sh             # start everything
#   ./scripts/start-mcpwrap.sh --verbose   # ... and tail the mcpwrap daemon log
#   ./scripts/stop-mcpwrap.sh              # stop everything
#
# Prerequisites:
#   - Go 1.24+ (only to build the wrapper; the binary is ~10 MB, no runtime deps)
#   - hosts entry: add `127.0.0.1 keycloak` so the browser OAuth flow
#                  (http://keycloak:8080/...) works from the host.
#
# Why this script exists:
#   mcpwrap's proxies are host processes (`mcpwrap up` runs in the
#   foreground) — they die when the parent terminal closes. This script
#   starts the daemon detached (nohup, pid in logs/mcpwrap.pid) and waits
#   until the three proxies answer /healthz before bringing the gateway up.
#   Re-run it after a reboot to bring the proxies back without touching the
#   gateway container.
#
# Container math (verified 3 containers on the host, no sidecars):
#   mcpwrap-memory             -> --network none (no outbound)
#   mcpwrap-fetch              -> default Docker networking (needs internet)
#   mcpwrap-sequentialthinking -> --network none (no outbound)
set -euo pipefail
cd "$(dirname "$0")/.."

VERBOSE=0
[[ "${1:-}" == "--verbose" ]] && VERBOSE=1

MCPWRAP_DIR="mcpwrap"
MCPWRAP_BIN="$MCPWRAP_DIR/mcpwrap.exe"
MCPWRAP_CONF="$MCPWRAP_DIR/mcpwrap.json"
MCPWRAP_PID="logs/mcpwrap.pid"
MCPWRAP_LOG="logs/mcpwrap.log"
PORTS=(19101 19102 19103)

mkdir -p logs

clean_state() {
  # kill a stale daemon from a previous session (proxies die on reboot anyway)
  if [[ -f "$MCPWRAP_PID" ]]; then
    kill -TERM "$(cat "$MCPWRAP_PID")" >/dev/null 2>&1 || true
    rm -f "$MCPWRAP_PID"
  fi
  # stop leftover containers from a hard crash
  docker ps -aq --filter "name=mcpwrap-" 2>/dev/null | xargs -r docker rm -f >/dev/null 2>&1 || true
  # clear stale workload state
  "$MCPWRAP_BIN" down >/dev/null 2>&1 || true
}

echo "==> [1/5] Prerequisites"
if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: 'docker' not found" >&2
  exit 1
fi
docker --version

if [[ ! -x "$MCPWRAP_BIN" ]]; then
  echo "==> Building mcpwrap (go build ./cmd/mcpwrap)..."
  (cd "$MCPWRAP_DIR" && go build -o mcpwrap.exe ./cmd/mcpwrap)
fi
if [[ ! -x "$MCPWRAP_BIN" ]]; then
  echo "ERROR: could not build $MCPWRAP_BIN (need Go 1.24+)" >&2
  exit 1
fi

# Loki docker log driver: the gateway container's stdout streams to Loki
# through it (deployments/docker-compose.mcpwrap.yml logging.driver=loki). Idempotent.
if ! docker plugin ls --format '{{.Name}}' | grep -q '^loki'; then
  echo "==> Installing Docker loki log driver plugin..."
  docker plugin install grafana/loki-docker-driver:latest --alias loki --grant-all-permissions
else
  echo "==> Docker loki log driver already installed."
fi

echo "==> [2/5] Remove stale mcpwrap state (containers may be gone after reboot)"
clean_state

echo "==> [3/5] Start MCP workloads on the host (mcpwrap daemon, detached)"
nohup "$MCPWRAP_BIN" up -f "$MCPWRAP_CONF" >"$MCPWRAP_LOG" 2>&1 &
echo $! > "$MCPWRAP_PID"
echo "  mcpwrap daemon pid $(cat "$MCPWRAP_PID") — log: $MCPWRAP_LOG"

echo "==> [4/5] Wait for the proxies (:19101/:19102/:19103 /healthz)"
for port in "${PORTS[@]}"; do
  ready=0
  for i in $(seq 1 30); do
    if curl -fsS "http://localhost:$port/healthz" >/dev/null 2>&1; then
      echo "  :$port/mcp ready"
      ready=1
      break
    fi
    sleep 1
  done
  if [[ "$ready" -eq 0 ]]; then
    echo "ERROR: proxy :$port did not become ready — tail of $MCPWRAP_LOG:" >&2
    tail -30 "$MCPWRAP_LOG" >&2 || true
    exit 1
  fi
done

echo "==> [5/5] Start gateway + Keycloak + observability"
# Volume ownership: the STOCK agentgateway image does NOT create
# /var/log/agentgateway, so a fresh `gateway-logs` named volume is
# root-owned and the gateway (uid 65532, read-only rootfs) crashes at
# startup with "failed to connect sqlite database". chown it to 65532 so it
# can create its SQLite request-log DB. Keycloak (official image runs as
# uid 1000) needs its persistent data dir writable for the same reason.
GATEWAY_LOGS_VOL="docker-compose-mcpwrap_gateway-logs"
KEYCLOAK_DATA_VOL="docker-compose-mcpwrap_keycloak-data"
docker volume create "$GATEWAY_LOGS_VOL" >/dev/null 2>&1 || true
docker volume create "$KEYCLOAK_DATA_VOL" >/dev/null 2>&1 || true
MSYS_NO_PATHCONV=1 docker run --rm -v "$GATEWAY_LOGS_VOL":/v alpine chown -R 65532:65532 /v
MSYS_NO_PATHCONV=1 docker run --rm -v "$KEYCLOAK_DATA_VOL":/v alpine chown -R 1000:1000 /v

docker compose -f deployments/docker-compose.mcpwrap.yml up -d

echo ""
echo "Gateway endpoints:"
echo "  :18080 /memory /fetch /thinking /mcp   (apiKey: sk-mcp-gateway-demo-key)"
echo "  :8082  /memory /fetch /thinking /mcp   (SSO: Keycloak OAuth, mcpAuthentication strict)"
echo "  Admin UI   -> http://localhost:15000/ui"
echo "  Grafana    -> http://localhost:3000 (admin/admin)"
echo "  Keycloak   -> http://localhost:8080 (admin/admin), realm mcp-demo"
echo "  Langfuse   -> http://localhost:3001 (admin@langfuse.local / admin123)"
echo "  Phoenix    -> http://localhost:6006"
echo ""
echo "Stop everything with:  ./scripts/stop-mcpwrap.sh"
echo "Then in VS Code Copilot: agent picker -> memory/fetch/thinking -> list your tools"
if [[ "$VERBOSE" -eq 1 ]]; then
  echo ""
  echo "==> mcpwrap daemon log (tail, Ctrl-C to stop) =="
  tail -f "$MCPWRAP_LOG"
fi
