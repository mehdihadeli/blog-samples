#!/usr/bin/env bash
# =============================================================================
# get-mcp-token.sh — mint a Keycloak access token for YOUR user for manual
# curl testing against the :8082 SSO port (mcpAuthentication).
#
# Usage (Linux/macOS):
#   ./scripts/get-mcp-token.sh alice
#   # or with the password on stdin:
#   echo 'alice123' | ./scripts/get-mcp-token.sh alice
#
# Each developer uses their OWN Keycloak user (alice, bob, ... — create more
# in the admin console at http://localhost:8081). Never share one token.
#
# NOTE: `mcp-gateway` is a PUBLIC PKCE client (no secret) — the password
# grant still works because directAccessGrantsEnabled is on.
# =============================================================================
set -euo pipefail
cd "$(dirname "$0")/.."

USERNAME="${1:-${MCP_USER:-}}"
KEYCLOAK_BASE="${KEYCLOAK_BASE:-http://localhost:8081}"
REALM="${KC_REALM:-mcp-demo}"
CLIENT_ID="${KC_CLIENT_ID:-mcp-gateway}"

if [[ -z "$USERNAME" ]]; then
    read -r -p "Keycloak username: " USERNAME
fi
read -r -s -p "Keycloak password for $USERNAME: " PASSWORD
echo

TOKEN="$(curl -fsS -X POST "$KEYCLOAK_BASE/realms/$REALM/protocol/openid-connect/token" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "client_id=$CLIENT_ID" \
    --data-urlencode "username=$USERNAME" \
    --data-urlencode "password=$PASSWORD" \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")"

echo "$TOKEN"
echo "" >&2
echo "Token (use as: Authorization: Bearer $TOKEN)" >&2
