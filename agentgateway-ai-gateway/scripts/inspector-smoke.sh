#!/usr/bin/env bash
# Exercise the Docker Compose MCP gateway through the official MCP Inspector CLI.
set -euo pipefail

MCP_URL="${MCP_URL:-http://localhost:3000/mcp}"
: "${KEYCLOAK_TOKEN:?Set KEYCLOAK_TOKEN to an Alice Keycloak access token}"

INSPECTOR=(npx --yes @modelcontextprotocol/inspector --cli \
  --transport http --server-url "$MCP_URL" --format json)
HEADER="Authorization: Bearer $KEYCLOAK_TOKEN"

echo "==> MCP Inspector: tools/list"
"${INSPECTOR[@]}" --method tools/list --header "$HEADER" | tee /tmp/agentgateway-tools.json
grep -q 'openapi_getInventory' /tmp/agentgateway-tools.json
grep -q 'tickets_tickets_list' /tmp/agentgateway-tools.json

echo "==> MCP Inspector: tickets_tickets_list"
"${INSPECTOR[@]}" --method tools/call \
  --tool-name tickets_tickets_list \
  --tool-args-json '{"status":"open"}' \
  --header "$HEADER"

echo "==> MCP Inspector: openapi_getInventory"
"${INSPECTOR[@]}" --method tools/call \
  --tool-name openapi_getInventory \
  --header "$HEADER"

echo "Inspector smoke checks passed."