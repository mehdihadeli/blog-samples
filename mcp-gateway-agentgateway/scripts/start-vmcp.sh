#!/usr/bin/env bash
# start-vmcp.sh — start the ToolHive Virtual MCP Server (vMCP) sample variant
# (approach 4 in the article: ToolHive's OWN gateway, no agentgateway at all).
#
# What runs where:
#   1. ToolHive (thv) MCP workloads on the HOST, in the `mcp-vmcp` group —
#      memory :19011, fetch :19012, sequentialthinking :19013, everything
#      :19014 (each proxied stdio -> Streamable HTTP; everything is built
#      on demand from the npm package via npx://).
#   2. Keycloak in Docker (deployments/docker-compose.vmcp.yml) — OIDC IdP,
#      realm mcp-demo, public PKCE client mcp-gateway (the SAME realm/client the
#      agentgateway SSO port uses, so one minted token works for both).
#   3. `thv vmcp serve` on the HOST — the vMCP aggregates the 4 workloads
#      into ONE unified endpoint http://127.0.0.1:4483/mcp and enforces
#      OIDC (Keycloak JWTs) on every client request per deployments/vmcp.yaml.
#
# Usage:
#   ./scripts/start-vmcp.sh             # start everything
#   ./scripts/start-vmcp.sh --verbose   # ... and tail the vMCP log
#   ./scripts/stop-vmcp.sh              # stop everything
#
# Prerequisites:
#   - ToolHive:   winget install stacklok.thv (Windows) / brew install thv (macOS)
#   - hosts entry: add `127.0.0.1 keycloak` so the vMCP (running on the host)
#                  can resolve the issuer http://keycloak:8080/realms/mcp-demo
#                  from deployments/vmcp.yaml (same entry the browser OAuth flow needs).
#
# Container math (verified 5 host containers, all but keycloak from thv):
#   mcp-fetch              -> default isolation (--isolate-network true):
#                              fetch + fetch-egress (Squid) + fetch-dns (dnsmasq)
#   mcp-memory             -> --isolate-network=false: 1 container (no outbound)
#   mcp-sequentialthinking -> --isolate-network=false: 1 container (no outbound)
#   mcp-everything         -> npx:// build-on-demand, --isolate-network=false:
#                              1 container (echo/sample tools, no real outbound)
#
# Flags:
#   --transport stdio --proxy-mode streamable-http  REQUIRED — without them thv
#       assumes the image self-hosts HTTP, never attaches stdin, and the
#       stdio-only server exits immediately (restart loop, proxy 502).
#   --host 0.0.0.0  so the workloads/vMCP are reachable beyond loopback.
set -euo pipefail
cd "$(dirname "$0")/.."

VERBOSE=0
[[ "${1:-}" == "--verbose" ]] && VERBOSE=1

WORKLOADS=(mcp-fetch mcp-memory mcp-sequentialthinking mcp-everything)
GROUP=mcp-vmcp
VMCP_PORT=4483

clean_state() {
  # Stop a previous vMCP serve (best-effort: pid may be stale after reboot).
  if [[ -f logs/vmcp.pid ]]; then
    kill "$(cat logs/vmcp.pid)" >/dev/null 2>&1 || true
    rm -f logs/vmcp.pid
  fi
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
  # Recreate the group so `thv vmcp init`/serve never sees a stale one.
  thv group rm "$GROUP" >/dev/null 2>&1 || true
  thv group create "$GROUP" >/dev/null 2>&1 || true
}

wait_keycloak_ready() {
  echo "==> Waiting for Keycloak /health/ready (management :9000)..."
  for i in $(seq 1 60); do
    if docker exec mcp-agentgateway-keycloak sh -c \
        "exec 3<>/dev/tcp/localhost/9000 && printf 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && cat <&3 | grep -q '200 OK'" 2>/dev/null; then
      echo "    Keycloak ready."
      return 0
    fi
    sleep 2
  done
  echo "ERROR: Keycloak did not become healthy in time (check: docker logs mcp-agentgateway-keycloak)" >&2
  return 1
}

echo "==> [1/6] Prerequisites"
if ! command -v thv >/dev/null 2>&1; then
  echo "ERROR: 'thv' not found. Install: winget install stacklok.thv (Windows) / brew install thv (macOS)" >&2
  exit 1
fi
thv version
docker --version

# Windows hosts file check: thv's Go resolver parses the FIRST line of the
# hosts file, so a UTF-8 BOM at the start of the file silently invalidates the
# `127.0.0.1 keycloak` entry (Go sees "\ufeff127.0.0.1", not a valid IP) and
# the vMCP falls back to DNS -> "no such host" on every token request.
# curl/browsers tolerate the BOM, Go does not. Fix (run as admin):
#   powershell -Command "[IO.File]::WriteAllBytes('C:\Windows\System32\drivers\etc\hosts',[IO.File]::ReadAllBytes('C:\Windows\System32\drivers\etc\hosts')[3..1e6])"
HOSTS_FILE="${WINDIR:-/c/Windows}/System32/drivers/etc/hosts"
if [[ -f "$HOSTS_FILE" ]] && head -c 3 "$HOSTS_FILE" | od -An -tx1 | grep -qi 'ef bb bf'; then
  echo "ERROR: $HOSTS_FILE starts with a UTF-8 BOM (ef bb bf) — thv cannot read the" >&2
  echo "       '127.0.0.1 keycloak' entry and the OIDC flow will fail with 'no such host'." >&2
  echo "       Fix (elevated PowerShell):" >&2
  echo "         powershell -Command \"[IO.File]::WriteAllBytes('C:\Windows\System32\drivers\etc\hosts',[IO.File]::ReadAllBytes('C:\Windows\System32\drivers\etc\hosts')[3..1e6])\"" >&2
  exit 1
fi
grep -qi '127\.0\.0\.1[[:space:]]keycloak' "$HOSTS_FILE" || {
  echo "ERROR: missing hosts entry '127.0.0.1 keycloak' in $HOSTS_FILE — required so" >&2
  echo "       the vMCP can resolve the issuer http://keycloak:8080/realms/mcp-demo" >&2
  exit 1
}

echo "==> [2/6] Remove stale workload state + recreate group '$GROUP'"
clean_state

echo "==> [3/6] Start MCP workloads in group '$GROUP' (5 containers total)"
thv run docker.io/mcp/fetch \
  --group "$GROUP" \
  --host 0.0.0.0 --proxy-port 19012 \
  --transport stdio --proxy-mode streamable-http
thv run docker.io/mcp/memory \
  --group "$GROUP" \
  --host 0.0.0.0 --proxy-port 19011 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
thv run docker.io/mcp/sequentialthinking \
  --group "$GROUP" \
  --host 0.0.0.0 --proxy-port 19013 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false
# everything has NO docker image (npx only). ToolHive's npx:// protocol scheme
# builds a container from the npm package on demand and runs it the same way.
thv run npx://@modelcontextprotocol/server-everything@latest \
  --name mcp-everything \
  --group "$GROUP" \
  --host 0.0.0.0 --proxy-port 19014 \
  --transport stdio --proxy-mode streamable-http \
  --isolate-network=false

echo "==> [4/6] Start Keycloak (deployments/docker-compose.vmcp.yml)"
# Volume ownership: the official Keycloak image runs as uid 1000 and needs
# its persistent data dir writable (see deployments/docker-compose.stdio.yml notes).
KEYCLOAK_DATA_VOL="docker-compose-vmcp_keycloak-data"
docker volume create "$KEYCLOAK_DATA_VOL" >/dev/null 2>&1 || true
MSYS_NO_PATHCONV=1 docker run --rm -v "$KEYCLOAK_DATA_VOL":/v alpine chown -R 1000:1000 /v
docker compose -f deployments/docker-compose.vmcp.yml up -d
wait_keycloak_ready

echo "==> [5/6] Start the Virtual MCP Server (aggregates the 4 workloads)"
thv vmcp validate --config deployments/vmcp.yaml
mkdir -p logs
nohup thv vmcp serve --config deployments/vmcp.yaml --host 0.0.0.0 --port "$VMCP_PORT" \
  > logs/vmcp.log 2>&1 &
echo $! > logs/vmcp.pid

echo "==> [6/6] Verify the vMCP endpoint"
for i in $(seq 1 30); do
  if curl -sf "http://127.0.0.1:$VMCP_PORT/health" >/dev/null 2>&1; then
    echo "    vMCP ready on http://127.0.0.1:$VMCP_PORT/mcp"
    break
  fi
  sleep 2
done
thv list
docker ps --format '{{.Names}}\t{{.Status}}' | grep -E 'mcp-(fetch|memory|sequentialthinking|everything)|keycloak' || true

echo ""
echo "vMCP endpoints (OIDC-protected, Keycloak realm mcp-demo / client mcp-gateway):"
echo "  http://127.0.0.1:$VMCP_PORT/mcp      (unified endpoint, all 4 servers, tools prefixed mcp-<workload>_)"
echo "  http://127.0.0.1:$VMCP_PORT/health   (vMCP health)"
echo "  Keycloak   -> http://localhost:8080 (admin/admin), realm mcp-demo"
echo ""
echo "Mint a token (password grant, aud=mcp-gateway) and list tools:"
echo "  TOKEN=\$(curl -s -X POST http://localhost:8080/realms/mcp-demo/protocol/openid-connect/token \\"
echo "    -d grant_type=password -d client_id=mcp-gateway \\"
echo "    -d username=mcpuser -d password=mcpuser123 | python -c 'import sys,json;print(json.load(sys.stdin)[\"access_token\"])')"
echo "  curl -s -X POST http://127.0.0.1:$VMCP_PORT/mcp -H \"Authorization: Bearer \$TOKEN\" \\"
echo "    -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \\"
echo "    -d '{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}'"
echo ""
echo "Stop everything with:  ./scripts/stop-vmcp.sh"
if [[ "$VERBOSE" -eq 1 ]]; then
  echo ""
  echo "==> vMCP log (tail, Ctrl-C to stop) =="
  tail -f logs/vmcp.log
fi
