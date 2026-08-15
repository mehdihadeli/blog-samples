#!/usr/bin/env bash
# start-toolhive.sh — start the full agentgateway MCP gateway sample.
#
# Three steps, all in one script:
#   1. ToolHive (thv) MCP workloads on the HOST — memory :19001, fetch :19002,
#      sequentialthinking :19003, everything :19004 (each proxied stdio ->
#      Streamable HTTP). `everything` has no docker image, so ToolHive builds a
#      container from the npm package on demand (npx:// protocol scheme).
#   2. Loki docker log driver plugin (idempotent) — the gateway container's
#      stdout streams to Loki through it (logging.driver=loki in compose).
#   3. docker compose -f docker-compose.toolhive.yml up -d — the gateway
#      (STOCK agentgateway image, no docker.sock) + Keycloak + observability
#      (otel-collector, prometheus, tempo, loki, grafana, langfuse, phoenix).
#
# Usage:
#   ./scripts/start-toolhive.sh             # start everything
#   ./scripts/start-toolhive.sh --verbose   # ... and tail the proxy logs
#   ./scripts/stop-toolhive.sh              # stop everything
#
# Prerequisites:
#   - ToolHive:   winget install stacklok.thv (Windows) / brew install thv (macOS)
#   - hosts entry: add `127.0.0.1 keycloak` so the browser OAuth flow
#                  (http://keycloak:8080/...) works from the host.
#
# Why this script exists:
#   thv run spawns a detached manager process (`thv start --foreground`) that
#   owns each HTTP proxy and dies when its parent terminal closes. After a
#   reboot / terminal close the gateway is up but the proxies are gone ->
#   `Connection refused` on :8082/* (HTTP 500 upstream). Re-run this script to
#   bring the proxies back without touching the gateway container.
#
# Container math (verified 5 -> 7 host containers after adding everything):
#   mcp-fetch              -> default isolation (--isolate-network true):
#                              fetch + fetch-egress (Squid) + fetch-dns (dnsmasq)
#                              [fetch needs internet]
#   mcp-memory             -> --isolate-network=false: 1 container (no outbound)
#   mcp-sequentialthinking -> --isolate-network=false: 1 container (no outbound)
#   mcp-everything         -> npx:// build-on-demand, --isolate-network=false:
#                              1 container (echo/sample tools, no real outbound)
#
# Flags:
#   --transport stdio --proxy-mode streamable-http  REQUIRED — without them thv
#       assumes the image self-hosts HTTP, never attaches stdin, and the
#       stdio-only server exits immediately (restart loop, proxy 502).
#   --host 0.0.0.0  so the gateway container (host.docker.internal) can reach
#       the proxy; default 127.0.0.1 is loopback-only.
set -euo pipefail
cd "$(dirname "$0")/.."

VERBOSE=0
[[ "${1:-}" == "--verbose" ]] && VERBOSE=1

WORKLOADS=(mcp-fetch mcp-memory mcp-sequentialthinking mcp-everything)

clean_state() {
  # thv rm fails silently when the container no longer exists; deleting the
  # runconfig/status files clears the "already exists" name conflicts.
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
  # Windows thv stores state under %LOCALAPPDATA%\toolhive
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
}

echo "==> [1/5] Prerequisites"
if ! command -v thv >/dev/null 2>&1; then
  echo "ERROR: 'thv' not found. Install: winget install stacklok.thv (Windows) / brew install thv (macOS)" >&2
  exit 1
fi
thv version
docker --version

# Loki docker log driver: the gateway container's stdout streams to Loki
# through it (docker-compose.toolhive.yml logging.driver=loki). Idempotent.
if ! docker plugin ls --format '{{.Name}}' | grep -q '^loki'; then
  echo "==> Installing Docker loki log driver plugin..."
  docker plugin install grafana/loki-docker-driver:latest --alias loki --grant-all-permissions
else
  echo "==> Docker loki log driver already installed."
fi

echo "==> [2/5] Remove stale workload state (containers may be gone after reboot)"
clean_state

echo "==> [3/5] Start MCP workloads on the host (7 containers total)"
thv run docker.io/mcp/fetch \
  --host 0.0.0.0 --proxy-port 19002 \
  --transport stdio --proxy-mode streamable-http
thv run docker.io/mcp/memory \
  --host 0.0.0.0 --proxy-port 19001 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
thv run docker.io/mcp/sequentialthinking \
  --host 0.0.0.0 --proxy-port 19003 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
# everything has NO docker image (npx only). ToolHive's npx:// protocol scheme
# builds a container from the npm package on demand and runs it the same way
# (see https://docs.stacklok.com/toolhive/guides-cli/run-mcp-servers#run-a-server-using-protocol-schemes).
thv run npx://@modelcontextprotocol/server-everything@latest \
  --name mcp-everything \
  --host 0.0.0.0 --proxy-port 19004 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false

echo "==> [4/5] Verify workloads"
thv list
docker ps --format '{{.Names}}\t{{.Status}}' | grep -E 'mcp-(fetch|memory|sequentialthinking|everything)' || true

echo "==> [5/5] Start gateway + Keycloak + observability"
# Volume ownership: the STOCK agentgateway image does NOT create
# /var/log/agentgateway, so a fresh `gateway-logs` named volume is
# root-owned and the gateway (uid 65532, read-only rootfs) crashes at
# startup with "failed to connect sqlite database". chown it to 65532 so it
# can create its SQLite request-log DB. Keycloak (official image runs as
# uid 1000) needs its persistent data dir writable for the same reason.
GATEWAY_LOGS_VOL="docker-compose-toolhive_gateway-logs"
KEYCLOAK_DATA_VOL="docker-compose-toolhive_keycloak-data"
docker volume create "$GATEWAY_LOGS_VOL" >/dev/null 2>&1 || true
docker volume create "$KEYCLOAK_DATA_VOL" >/dev/null 2>&1 || true
MSYS_NO_PATHCONV=1 docker run --rm -v "$GATEWAY_LOGS_VOL":/v alpine chown -R 65532:65532 /v
MSYS_NO_PATHCONV=1 docker run --rm -v "$KEYCLOAK_DATA_VOL":/v alpine chown -R 1000:1000 /v

docker compose -f docker-compose.toolhive.yml up -d

echo ""
echo "Gateway endpoints:"
echo "  :18080 /memory /fetch /thinking /mcp   (apiKey: sk-mcp-gateway-demo-key; /mcp multiplexes all 4 servers)"
echo "  :8082  /memory /fetch /thinking /mcp   (SSO: Keycloak OAuth, mcpAuthentication strict; /mcp multiplexes all 4 servers)"
echo "  Admin UI   -> http://localhost:15000/ui"
echo "  Grafana    -> http://localhost:3000 (admin/admin)"
echo "  Keycloak   -> http://localhost:8080 (admin/admin), realm mcp-demo"
echo "  Langfuse   -> http://localhost:3001 (admin@langfuse.local / admin123)"
echo "  Phoenix    -> http://localhost:6006"
echo ""
echo "Stop everything with:  ./scripts/stop-toolhive.sh"
echo "Then in VS Code Copilot: agent picker -> memory/fetch/thinking -> list your tools"
if [[ "$VERBOSE" -eq 1 ]]; then
  echo ""
  echo "==> Proxy logs (tail, Ctrl-C to stop) =="
  tail -f "$HOME/.local/state/toolhive/logs"/*.log 2>/dev/null || \
  tail -f "$LOCAL_STATE/toolhive/logs"/*.log 2>/dev/null || true
fi
