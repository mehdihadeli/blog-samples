#!/usr/bin/env bash
set -euo pipefail

KUBECTL_DIR="${KUBECTL_DIR:-$HOME/.local/bin}"
KUBECTL_PATH="$KUBECTL_DIR/kubectl"

detect_os() {
  case "$(uname -s)" in
    Linux) echo linux ;;
    Darwin) echo darwin ;;
    *) echo "Unsupported OS: $(uname -s)" >&2; exit 1 ;;
  esac
}

detect_arch() {
  case "$(uname -m)" in
    x86_64|amd64) echo amd64 ;;
    arm64|aarch64) echo arm64 ;;
    *) echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
  esac
}

kubectl_ready() {
  command -v kubectl >/dev/null 2>&1 && kubectl version --client >/dev/null 2>&1
}

if kubectl_ready; then
  echo "kubectl already installed"
  exit 0
fi

mkdir -p "$KUBECTL_DIR"

HOST_OS="$(detect_os)"
HOST_ARCH="$(detect_arch)"
VERSION="$(curl -fsSL https://dl.k8s.io/release/stable.txt)"

curl -fsSLo kubectl "https://dl.k8s.io/release/${VERSION}/bin/${HOST_OS}/${HOST_ARCH}/kubectl"
chmod +x kubectl
mv kubectl "$KUBECTL_PATH"

case ":$PATH:" in
  *":$KUBECTL_DIR:"*) ;;
  *)
    if [ -f "$HOME/.bashrc" ] && ! grep -Fq "$KUBECTL_DIR" "$HOME/.bashrc"; then
      echo "export PATH=\"$KUBECTL_DIR:\$PATH\"" >> "$HOME/.bashrc"
    fi
    if [ -f "$HOME/.profile" ] && ! grep -Fq "$KUBECTL_DIR" "$HOME/.profile"; then
      echo "export PATH=\"$KUBECTL_DIR:\$PATH\"" >> "$HOME/.profile"
    fi
    export PATH="$KUBECTL_DIR:$PATH"
    ;;
esac

kubectl version --client