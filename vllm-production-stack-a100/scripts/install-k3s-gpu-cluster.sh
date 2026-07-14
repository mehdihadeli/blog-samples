#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GPU_OPERATOR_VERSION="${GPU_OPERATOR_VERSION:-v24.9.1}"
K3S_CONFIG_PATH="/etc/rancher/k3s/k3s.yaml"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing required command: $1" >&2
    exit 1
  }
}

install_nvidia_toolkit() {
  if command -v nvidia-ctk >/dev/null 2>&1; then
    return 0
  fi

  if [ ! -r /etc/os-release ]; then
    echo "Cannot detect OS for NVIDIA Container Toolkit installation." >&2
    exit 1
  fi

  . /etc/os-release
  case "$ID" in
    ubuntu|debian) ;;
    *)
      echo "Automatic NVIDIA Container Toolkit installation only handles Ubuntu/Debian. Install it manually first." >&2
      exit 1
      ;;
  esac

  curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | \
    sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

  curl -fsSL https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
    sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
    sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list >/dev/null

  sudo apt-get update
  sudo apt-get install -y nvidia-container-toolkit
}

if [ "$(uname -s)" != "Linux" ]; then
  echo "This script targets a Linux host running k3s directly on the GPU server." >&2
  exit 1
fi

bash "$SCRIPT_DIR/install-kubectl.sh"
bash "$SCRIPT_DIR/install-helm.sh"

require_cmd curl
require_cmd sudo
require_cmd systemctl
require_cmd nvidia-smi

sudo swapoff -a
echo "Swap disabled for current boot. Disable it permanently in /etc/fstab before relying on this node."

install_nvidia_toolkit

if ! command -v k3s >/dev/null 2>&1; then
  curl -sfL https://get.k3s.io | \
    INSTALL_K3S_EXEC="server --disable traefik --write-kubeconfig-mode 0644" \
    sh -
else
  echo "k3s already installed"
  sudo systemctl restart k3s
fi

mkdir -p "$HOME/.kube"
sudo cp "$K3S_CONFIG_PATH" "$HOME/.kube/config"
sudo chown "$(id -u):$(id -g)" "$HOME/.kube/config"

export KUBECONFIG="$HOME/.kube/config"

helm repo add nvidia https://helm.ngc.nvidia.com/nvidia >/dev/null 2>&1 || true
helm repo update >/dev/null

if ! kubectl get namespace gpu-operator >/dev/null 2>&1; then
  helm install --wait gpu-operator -n gpu-operator --create-namespace nvidia/gpu-operator --version "$GPU_OPERATOR_VERSION"
else
  helm upgrade --wait gpu-operator -n gpu-operator nvidia/gpu-operator --version "$GPU_OPERATOR_VERSION"
fi

kubectl get nodes -o wide
kubectl get pods -n gpu-operator
echo "Check GPU capacity with: kubectl get nodes -o jsonpath='{.items[0].status.allocatable.nvidia\.com/gpu}'"
echo "Deploy this sample with: helm install vllm ./helm -f values.yaml -f values.k3s.yaml"