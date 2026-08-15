#!/usr/bin/env bash
# install.sh — build mcpwrap and put the `mcpwrap` binary on your PATH.
#
# Usage:
#   ./install.sh                      # build from this repo -> ~/.local/bin
#   ./install.sh --prefix ~/bin       # install to a custom directory
#   ./install.sh --go                 # go install ./cmd/mcpwrap -> $GOBIN/$GOPATH/bin
#   ./install.sh --remote             # go install from the published module
#
# After install, open a new terminal and run:
#   mcpwrap run docker.io/mcp/memory --port 19101 --no-network
set -euo pipefail
cd "$(dirname "$0")"

MODULE="github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap"
BIN="mcpwrap"
GOBIN_PATH="$(go env GOBIN 2>/dev/null || true)"
[[ -z "$GOBIN_PATH" ]] && GOBIN_PATH="$(go env GOPATH)/bin"
VERSION="$(git describe --tags --always --dirty 2>/dev/null || echo dev)"

MODE="local"
PREFIX=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --prefix) PREFIX="${2:-}"; shift ;;
    --go)     MODE="go" ;;
    --remote) MODE="remote" ;;
    -h|--help) sed -n '2,8p' "$0"; exit 0 ;;
    *) echo "unknown option: $1 (see ./install.sh --help)" >&2; exit 1 ;;
  esac
  shift
done

case "$MODE" in
  remote)
    echo "==> go install $MODULE/cmd/mcpwrap@latest"
    go install "$MODULE/cmd/mcpwrap@latest"
    DEST="$GOBIN_PATH"
    ;;
  go)
    echo "==> go install ./cmd/mcpwrap (ldflags version=$VERSION)"
    go install -ldflags "-s -w -X $MODULE/internal/cli.Version=$VERSION" ./cmd/mcpwrap
    DEST="$GOBIN_PATH"
    ;;
  local)
    DEST="${PREFIX:+$PREFIX/bin}"
    [[ -z "$DEST" ]] && DEST="$HOME/.local/bin"
    mkdir -p "$DEST"
    echo "==> go build -o $DEST/$BIN ./cmd/mcpwrap (version=$VERSION)"
    go build -trimpath -ldflags "-s -w -X $MODULE/internal/cli.Version=$VERSION" -o "$DEST/$BIN" ./cmd/mcpwrap
    ;;
esac

echo ""
echo "Installed: $DEST/$BIN"
"$DEST/$BIN" version
echo ""
if [[ ":$PATH:" == *":$DEST:"* ]]; then
  echo "==> $DEST is already on your PATH — open a new terminal and run: mcpwrap --help"
else
  echo "==> Add $DEST to your PATH, e.g.:"
  echo "      export PATH=\"$DEST:\$PATH\"   # bash/zsh"
  echo "      fish_add_path $DEST            # fish"
  echo "      [Environment]::SetEnvironmentVariable('Path', \"$DEST;\" + \$env:Path, 'User')  # PowerShell"
fi
