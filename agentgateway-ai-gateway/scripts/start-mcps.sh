#!/usr/bin/env bash
# start-mcps.sh - start the external MCP server (everything) with
# ToolHive on the HOST, then bring up the full agentgateway compose stack.
#
# Why ToolHive instead of containers in the compose file?
#   - The reference MCP server (everything) is stdio-only by
#     default; `thv run` wraps each one in a Streamable HTTP proxy
#     (--transport stdio --proxy-mode streamable-http) and keeps the
#     workload state on the host, outside the compose stack.
#   - `thv run` spawns a detached manager process that owns the proxy and
#     dies when its parent terminal closes. After a reboot / terminal close
#     the gateway container is up but the proxies are gone -> "Connection
#     refused" (HTTP 500 upstream). Re-run this script to bring the proxies
#     back without touching the gateway.
#   - Networking isolation: thv defaults to --isolate-network=true, which
#     spawns THREE containers per workload (workload + egress Squid proxy +
#     dnsmasq DNS). Only enable it when the MCP actually needs outbound
#     internet (e.g. the fetch server). everything needs no outbound
#     traffic, so we pass --isolate-network=false and keep ONE container per
#     workload.
#
# Usage:
#   ./scripts/start-mcps.sh              # start everything (local rate limits)
#   ./scripts/start-mcps.sh --ratelimit  # ... with per-user Envoy rate limits
#   ./scripts/start-mcps.sh --verbose    # ... and tail the proxy logs
#   ./scripts/stop-mcps.sh               # stop everything
#
# Rate limiting (optional Envoy): by default the gateway uses LOCAL in-memory
# token buckets (agentgateway-config.yaml). Pass --ratelimit to ALSO mount
# docker-compose.ratelimit.yaml (adds the Envoy ratelimit service + Redis)
# and switch the gateway to the remote-ratelimit config variant
# (agentgateway-config.remote-ratelimit.yaml).
#
# Prerequisites:
#   - ToolHive: winget install stacklok.thv (Windows) / brew install thv (macOS)
#   - The compose project name is `agentgateway-ai-gateway` (set in
#     deploy/docker-compose.yaml) so the volume chowns below work.
set -euo pipefail
cd "$(dirname "$0")/.."

VERBOSE=0
RATELIMIT=0
for arg in "$@"; do
  case "$arg" in
    --verbose) VERBOSE=1 ;;
    --ratelimit) RATELIMIT=1 ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

COMPOSE_FILES="-f deploy/docker-compose.yaml"
if [[ "$RATELIMIT" -eq 1 ]]; then
  COMPOSE_FILES="$COMPOSE_FILES -f deploy/docker-compose.ratelimit.yaml"
fi

WORKLOADS=(mcp-everything)

clean_state() {
  # thv rm fails silently when the container no longer exists; deleting the
  # runconfig/status files clears the "already exists" name conflicts.
  for w in "${WORKLOADS[@]}"; do
    thv rm "$w" >/dev/null 2>&1 || true
    docker rm -f "$w" >/dev/null 2>&1 || true
  done
  rm -f \
    "$HOME/.local/state/toolhive/runconfigs/mcp-everything.json" \
    "$HOME/.local/state/toolhive/statuses/mcp-everything.json"
  # Windows thv stores state under %LOCALAPPDATA%\toolhive
  LOCAL_STATE="${LOCALAPPDATA:-}"
  if [[ -n "$LOCAL_STATE" ]]; then
    rm -f \
      "$LOCAL_STATE/toolhive/runconfigs/mcp-everything.json" \
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

echo "==> [2/5] Remove stale workload state (containers may be gone after reboot)"
clean_state

echo "==> [3/5] Start MCP workloads on the host (ToolHive proxies)"
# everything - the reference MCP server, stdio-only. ToolHive's npx://
# protocol scheme builds a container from the npm package on demand
# (see https://docs.stacklok.com/toolhive/guides-cli/run-mcp-servers).
thv run npx://@modelcontextprotocol/server-everything@latest \
  --name mcp-everything \
  --host 0.0.0.0 --proxy-port 19101 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
echo "==> [4/5] Verify workloads"
thv list
docker ps --format '{{.Names}}\t{{.Status}}' | grep -E 'mcp-everything' || true

echo "==> [5/5] Start gateway + Keycloak + observability"
# Volume ownership: the STOCK agentgateway image does NOT create
# /var/log/agentgateway, so a fresh `gateway-logs` named volume is
# root-owned and the gateway (uid 65532, read-only rootfs) crashes at
# startup with "failed to connect sqlite database". chown it to 65532 so it
# can create its SQLite request-log DB. Keycloak (official image runs as
# uid 1000) needs its persistent data dir writable for the same reason.
GATEWAY_LOGS_VOL="agentgateway-ai-gateway_gateway-logs"
KEYCLOAK_DATA_VOL="agentgateway-ai-gateway_keycloak-data"
docker volume create "$GATEWAY_LOGS_VOL" >/dev/null 2>&1 || true
docker volume create "$KEYCLOAK_DATA_VOL" >/dev/null 2>&1 || true
MSYS_NO_PATHCONV=1 docker run --rm -v "$GATEWAY_LOGS_VOL":/v alpine chown -R 65532:65532 /v
MSYS_NO_PATHCONV=1 docker run --rm -v "$KEYCLOAK_DATA_VOL":/v alpine chown -R 1000:1000 /v

docker compose $COMPOSE_FILES up -d --build

echo ""
echo "Gateway endpoints:"
echo "  :3000  MCP gateway (multiplexes tickets/catalog/customers/everything/time, Keycloak JWT required)"
echo "  :4000  LLM gateway (DeepSeek + virtual keys)"
echo "  :3001  A2A gateway (support-agent card + /v1/message:send)"
echo "  Admin UI   -> http://localhost:15000/ui  (CEL playground at /ui/cel/)"
echo "  Grafana    -> http://localhost:13000 (admin/admin)"
echo "  Keycloak   -> http://localhost:8080 (admin/admin), realm agentgateway"
echo "  Langfuse   -> http://localhost:13001 (admin@example.com / admin-password)"
if [[ "$RATELIMIT" -eq 1 ]]; then
  echo "  Ratelimit  -> Envoy ratelimit service up (per-user limits in infra/ratelimit/config.yaml)"
else
  echo "  Rate limits-> LOCAL in-memory token buckets (start with --ratelimit for Envoy per-user limits)"
fi
echo ""
echo "Stop everything with:  ./scripts/stop-mcps.sh"
if [[ "$VERBOSE" -eq 1 ]]; then
  echo ""
  echo "==> Proxy logs (tail, Ctrl-C to stop) =="
  tail -f "$HOME/.local/state/toolhive/logs"/*.log 2>/dev/null || \
  tail -f "$LOCAL_STATE/toolhive/logs"/*.log 2>/dev/null || true
fi
