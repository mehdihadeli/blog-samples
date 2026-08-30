#!/usr/bin/env bash
# start-mcps.sh - start external MCP servers (everything and
# sequentialthinking) with
# ToolHive on the HOST.
#
# Why ToolHive instead of containers in the compose file?
#   - External MCP servers are stdio-only by
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
#   ./scripts/start-mcps.sh            # start the MCP proxy
#   ./scripts/start-mcps.sh --verbose  # ... and tail the proxy logs
#
# Start the gateway stack separately with Docker Compose from the repository
# root. See deployments/docker-compose.yaml and deployments/docker-compose.ratelimit.yaml.
#
# Prerequisites:
#   - ToolHive: winget install stacklok.thv (Windows) / brew install thv (macOS)
#   - The compose project name is `agentgateway-ai-gateway` (set in
#     deployments/docker-compose.yaml) so the volume chowns below work.
set -euo pipefail
cd "$(dirname "$0")/.."

VERBOSE=0
for arg in "$@"; do
  case "$arg" in
    --verbose) VERBOSE=1 ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done

WORKLOADS=(mcp-everything mcp-sequentialthinking)

clean_state() {
  # thv rm fails silently when the container no longer exists; deleting the
  # runconfig/status files clears the "already exists" name conflicts.
  for w in "${WORKLOADS[@]}"; do
    thv rm "$w" >/dev/null 2>&1 || true
    docker rm -f "$w" >/dev/null 2>&1 || true
  done
  for w in "${WORKLOADS[@]}"; do
    rm -f \
      "$HOME/.local/state/toolhive/runconfigs/$w.json" \
      "$HOME/.local/state/toolhive/statuses/$w.json"
  done
  # Windows thv stores state under %LOCALAPPDATA%\toolhive
  LOCAL_STATE="${LOCALAPPDATA:-}"
  if [[ -n "$LOCAL_STATE" ]]; then
    for w in "${WORKLOADS[@]}"; do
      rm -f \
        "$LOCAL_STATE/toolhive/runconfigs/$w.json" \
        "$LOCAL_STATE/toolhive/statuses/$w.json"
    done
  fi
}

echo "==> [1/4] Prerequisites"
if ! command -v thv >/dev/null 2>&1; then
  echo "ERROR: 'thv' not found. Install: winget install stacklok.thv (Windows) / brew install thv (macOS)" >&2
  exit 1
fi
thv version
docker --version

echo "==> [2/4] Remove stale workload state (containers may be gone after reboot)"
clean_state

echo "==> [3/4] Start MCP workloads on the host (ToolHive proxies)"
# everything - the reference MCP server from Docker Hub.
thv run docker.io/mcp/everything:latest \
  --name mcp-everything \
  --host 0.0.0.0 --proxy-port 19101 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
# sequentialthinking - the Docker-hosted stdio MCP server from Docker Hub.
thv run docker.io/mcp/sequentialthinking:latest \
  --name mcp-sequentialthinking \
  --host 0.0.0.0 --proxy-port 19103 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
echo "==> [4/4] Verify workloads"
thv list
docker ps --format '{{.Names}}\t{{.Status}}' | grep -E 'mcp-(everything|sequentialthinking)' || true

echo ""
echo "MCP proxies started on http://localhost:19101 and http://localhost:19103."
echo "Start Docker Compose manually when needed."
if [[ "$VERBOSE" -eq 1 ]]; then
  echo ""
  echo "==> Proxy logs (tail, Ctrl-C to stop) =="
  tail -f "$HOME/.local/state/toolhive/logs"/*.log 2>/dev/null || \
  tail -f "$LOCAL_STATE/toolhive/logs"/*.log 2>/dev/null || true
fi
