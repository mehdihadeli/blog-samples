#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MINIKUBE_MEMORY="${MINIKUBE_MEMORY:-32768}"
MINIKUBE_CPUS="${MINIKUBE_CPUS:-12}"
MINIKUBE_PROFILE="${MINIKUBE_PROFILE:-minikube}"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing required command: $1" >&2
    exit 1
  }
}

if [ "$(uname -s)" != "Linux" ]; then
  echo "This script targets a Linux host with Docker GPU support." >&2
  exit 1
fi

bash "$SCRIPT_DIR/install-kubectl.sh"
bash "$SCRIPT_DIR/install-helm.sh"

require_cmd curl
require_cmd docker
require_cmd nvidia-smi
require_cmd nvidia-ctk
require_cmd sudo

if [ -f /proc/sys/net/core/bpf_jit_harden ]; then
  current_bpf="$(sysctl -n net.core.bpf_jit_harden || echo 1)"
  if [ "$current_bpf" != "0" ]; then
    echo "Setting net.core.bpf_jit_harden=0 for minikube GPU support"
    echo "net.core.bpf_jit_harden=0" | sudo tee -a /etc/sysctl.conf >/dev/null
    sudo sysctl -p >/dev/null
  fi
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not usable for the current user. Fix Docker access before continuing." >&2
  exit 1
fi

case "$(uname -m)" in
  x86_64|amd64) MINIKUBE_ARCH=amd64 ;;
  arm64|aarch64) MINIKUBE_ARCH=arm64 ;;
  *) echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

if ! command -v minikube >/dev/null 2>&1; then
  curl -fsSLo minikube "https://storage.googleapis.com/minikube/releases/latest/minikube-linux-${MINIKUBE_ARCH}"
  sudo install minikube /usr/local/bin/minikube
  rm -f minikube
fi

sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker

if minikube status -p "$MINIKUBE_PROFILE" >/dev/null 2>&1; then
  echo "Deleting existing minikube profile $MINIKUBE_PROFILE so GPU runtime changes apply cleanly"
  minikube delete -p "$MINIKUBE_PROFILE"
fi

minikube start \
  -p "$MINIKUBE_PROFILE" \
  --driver docker \
  --container-runtime docker \
  --gpus all \
  --memory "$MINIKUBE_MEMORY" \
  --cpus "$MINIKUBE_CPUS"

kubectl get nodes -o wide
echo "Check GPU capacity with: kubectl get nodes -o jsonpath='{.items[0].status.allocatable.nvidia\.com/gpu}'"
echo "Deploy this sample with: helm install vllm ./helm -f values.yaml -f values.minikube.yaml"