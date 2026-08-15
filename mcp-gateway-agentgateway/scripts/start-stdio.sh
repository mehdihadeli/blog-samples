#!/usr/bin/env bash
# start-stdio.sh — start the agentgateway MCP gateway sample with the NATIVE
# STDIO runtime (approach 1: agentgateway spawns the MCP servers itself as
# `stdio` subprocesses — no host runtime, no HTTP bridge, no gateway image).
#
# This variant runs the gateway as a HOST BINARY (the documented flow for
# `stdio` targets: install the binary, `agentgateway -f config.yaml` — see
# https://agentgateway.dev/docs/standalone/latest/mcp/connect/stdio/).
# There is NO gateway container and NO custom gateway image: the binary
# forks `npx @modelcontextprotocol/server-memory`,
# `uvx mcp-server-fetch` and `npx @modelcontextprotocol/server-sequential-thinking`
# on the host.
#
# Steps:
#   1. Install the agentgateway binary on the host if missing
#      (`curl -sL https://agentgateway.dev/install | bash`, or use an
#      existing `agentgateway` on PATH — tested with `agentgateway --version`).
#   2. Ensure the MCP server runners on the host (npx via Node, uvx via uv).
#   3. Loki docker log driver plugin (idempotent) — infra containers log to
#      Loki; the HOST gateway binary logs to logs/agentgateway-stdio.log.
#   4. Keycloak + observability compose stack (docker-compose.stdio.yml —
#      NO gateway service in this variant).
#   5. Start the gateway binary DETACHED on the host with config.stdio.yaml
#      (nohup, pid in logs/agentgateway-stdio.pid) and wait for :18080.
#
# Usage:
#   ./scripts/start-stdio.sh             # ensure runners + start everything
#   ./scripts/stop-stdio.sh              # stop everything
#
# Prerequisites:
#   - Docker (for the Keycloak + observability stack)
#   - Node.js + npm (for npx @modelcontextprotocol/server-memory and
#     @modelcontextprotocol/server-sequential-thinking)
#   - uv (for uvx mcp-server-fetch) — the script installs it via pip if
#     missing; otherwise `winget install astral-sh.uv` / `brew install uv`
#   - hosts entry: add `127.0.0.1 keycloak` so the browser OAuth flow
#     (http://keycloak:8080/...) works from the host.
#
# Why this script exists:
#   The gateway binary is a host process — it dies when the parent terminal
#   closes. This script starts it detached (nohup, pid file) and waits until
#   the apiKey port answers before returning. Re-run it after a reboot to
#   bring the gateway back without touching the compose stack.
#
# Container math (verified 0 gateway containers — infra only):
#   The gateway is a host process; each MCP server is a subprocess of it.
set -euo pipefail
cd "$(dirname "$0")/.."

GATEWAY_BIN="$(command -v agentgateway || true)"
GATEWAY_PID="logs/agentgateway-stdio.pid"
GATEWAY_LOG="logs/agentgateway-stdio.log"
mkdir -p logs

echo "==> [1/6] Prerequisites"
if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: 'docker' not found" >&2
  exit 1
fi
docker --version

if ! command -v pip3 >/dev/null 2>&1 && ! command -v pip >/dev/null 2>&1; then
  echo "ERROR: neither 'pip3' nor 'pip' found (needed to install uv/uvx)" >&2
  exit 1
fi
if ! command -v npx >/dev/null 2>&1; then
  echo "ERROR: 'npx' not found (needed for the memory/thinking servers)" >&2
  exit 1
fi

echo "==> [2/6] agentgateway binary on the host"
if [[ -z "$GATEWAY_BIN" ]]; then
  echo "  'agentgateway' not found on PATH — installing via https://agentgateway.dev/install"
  curl -sL https://agentgateway.dev/install | bash
  GATEWAY_BIN="$(command -v agentgateway || true)"
  if [[ -z "$GATEWAY_BIN" ]]; then
    echo "ERROR: install finished but 'agentgateway' still not on PATH" >&2
    echo "  (re-login / new shell, or add the install dir to PATH, then re-run)" >&2
    exit 1
  fi
fi
"$GATEWAY_BIN" --version

echo "==> [3/6] MCP server runners on the host (npx + uvx)"
# The `stdio` targets in config.stdio.yaml spawn these commands on the host:
#   npx -y @modelcontextprotocol/server-memory
#   npx -y @modelcontextprotocol/server-sequential-thinking
#   uvx --with "mcp<2" mcp-server-fetch
# npx and uvx fetch the packages on demand (no per-package install needed);
# uvx ships with Astral's `uv` — installed here via pip if missing. The
# `--with "mcp<2"` pin keeps mcp SDK <2 (McpError import compat).
if ! command -v uvx >/dev/null 2>&1; then
  echo "  'uvx' not found — installing uv via pip"
  if pip3 >/dev/null 2>&1; then PIP=pip3; else PIP=pip; fi
  "$PIP" install --quiet uv
fi
uvx --version
npx --version

# Loki docker log driver: infra container stdout -> Loki (idempotent).
if ! docker plugin ls --format '{{.Name}}' | grep -q '^loki'; then
  echo "==> Installing Docker loki log driver plugin..."
  docker plugin install grafana/loki-docker-driver:latest --alias loki --grant-all-permissions
else
  echo "==> Docker loki log driver already installed."
fi

echo "==> [4/6] Keycloak + observability (no gateway service in this variant)"
KEYCLOAK_DATA_VOL="docker-compose-stdio_keycloak-data"
docker volume create "$KEYCLOAK_DATA_VOL" >/dev/null 2>&1 || true
MSYS_NO_PATHCONV=1 docker run --rm -v "$KEYCLOAK_DATA_VOL":/v alpine chown -R 1000:1000 /v

docker compose -f docker-compose.stdio.yml up -d

echo "==> [5/6] Wait for Keycloak (the gateway fetches its JWKS at startup)"
for i in $(seq 1 60); do
  if docker inspect -f '{{.State.Health.Status}}' mcp-agentgateway-keycloak 2>/dev/null | grep -q healthy; then
    echo "  Keycloak healthy"
    break
  fi
  sleep 1
done
if ! docker inspect -f '{{.State.Health.Status}}' mcp-agentgateway-keycloak 2>/dev/null | grep -q healthy; then
  echo "ERROR: Keycloak did not become healthy — check 'docker ps' / 'docker logs mcp-agentgateway-keycloak'" >&2
  exit 1
fi

echo "==> [6/6] Start the host gateway binary (detached)"
# Clean a stale daemon from a previous session.
if [[ -f "$GATEWAY_PID" ]]; then
  kill -TERM "$(cat "$GATEWAY_PID")" >/dev/null 2>&1 || true
  rm -f "$GATEWAY_PID"
fi
# Create the request-log DB dir: a host binary resolves `sqlite:///tmp/...`
# to the CURRENT DRIVE root on Windows (e.g. D:\tmp\agentgateway when the
# gateway is started from D:) and /tmp/... on Linux. The gateway won't
# start without a writable parent dir.
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*)
    # MSYS path: /d/... = D:\. Extract the current drive letter.
    winroot="$(pwd | sed 's|^/\([A-Za-z]\)/.*|\1|')"
    mkdir -p "/${winroot}/tmp/agentgateway"
    ;;
  *) mkdir -p /tmp/agentgateway ;;
esac
nohup "$GATEWAY_BIN" -f config.stdio.yaml >"$GATEWAY_LOG" 2>&1 &
echo $! > "$GATEWAY_PID"
echo "  agentgateway pid $(cat "$GATEWAY_PID") — log: $GATEWAY_LOG"

echo "  Waiting for :18080 (apiKey gateway)..."
ready=0
# First stdio spawn runs `npx`/`uvx`, which downloads the server package —
# allow up to 120s (subsequent starts are fast, npx/uvx cache).
for i in $(seq 1 120); do
  if curl -fsS -o /dev/null "http://localhost:18080/memory" -X POST \
      -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
      -H "x-api-key: sk-mcp-gateway-demo-key" \
      -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}' >/dev/null 2>&1; then
    echo "  :18080/memory answered"
    ready=1
    break
  fi
  if (( i % 10 == 0 )); then echo "  ... still waiting ($i s)"; fi
  sleep 1
done
if [[ "$ready" -eq 0 ]]; then
  echo "ERROR: gateway did not become ready — tail of $GATEWAY_LOG:" >&2
  tail -30 "$GATEWAY_LOG" >&2 || true
  exit 1
fi

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
echo "Stop everything with:  ./scripts/stop-stdio.sh"
echo "Then in VS Code Copilot: agent picker -> memory/fetch/thinking -> list your tools"
