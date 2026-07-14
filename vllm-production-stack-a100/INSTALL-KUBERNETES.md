# Kubernetes Environment Setup for One A100

This guide is the bootstrap companion for the `vllm-production-stack-a100` sample. It fills the gap between a bare GPU server and the point where `helm install` makes sense.

It supports two single-node paths:

- `minikube` for a fast local cluster on a Linux host that already runs Docker with GPU access
- `k3s` for a lightweight always-on cluster on the same GPU server

If you already run a tuned Kubernetes cluster with MIG slices and NVIDIA runtime classes, you can skip this guide and go straight to `values.yaml` plus the sample README.

## What This Guide Installs

- `kubectl`
- `helm`
- one local Kubernetes distribution: `minikube` or `k3s`
- NVIDIA runtime support for the chosen path
- cluster-level GPU exposure so the sample can request `nvidia.com/gpu`

## Prerequisites

Before either path, verify the host itself is healthy.

- Linux host
- NVIDIA driver already installed
- one NVIDIA A100 visible on the host
- sudo access
- internet access for package downloads

Host-side checks:

```bash
nvidia-smi
```

If you plan to use `minikube`, also verify Docker can see the GPU:

```bash
docker run --rm --gpus all nvidia/cuda:12.4.1-base-ubuntu22.04 nvidia-smi
```

Do not continue until those commands work.

## Choose the Right Path

Use `minikube` when:

- Docker already works well on the host
- you want the quickest local cluster
- you are treating the cluster as disposable

Use `k3s` when:

- the GPU machine will behave like a small permanent node
- you want a lighter control plane than a full kubeadm stack
- you prefer the `containerd` plus GPU Operator path

## Step 1: Install `kubectl`

From the sample folder:

```bash
bash scripts/install-kubectl.sh
kubectl version --client
```

The script installs `kubectl` into `~/.local/bin` and uses the current stable release for the detected host platform.

## Step 2: Install `helm`

From the sample folder:

```bash
bash scripts/install-helm.sh
helm version
```

This uses the official Helm installation script.

## Step 3A: Create a GPU-enabled `minikube` cluster

This path follows the same basic shape as the upstream Production Stack utility flow.

What the script does:

- installs `kubectl` and `helm` if needed
- installs `minikube` if needed
- configures Docker through `nvidia-ctk runtime configure --runtime=docker`
- restarts Docker
- starts `minikube` with Docker driver and `--gpus all`

Run:

```bash
bash scripts/install-minikube-cluster.sh
```

Optional sizing overrides:

```bash
MINIKUBE_MEMORY=40960 MINIKUBE_CPUS=16 bash scripts/install-minikube-cluster.sh
```

If you already had a pre-GPU `minikube` profile, delete it first so the runtime changes take effect cleanly:

```bash
minikube delete
```

### Verify `minikube`

Check the cluster:

```bash
minikube status
kubectl get nodes -o wide
kubectl get nodes -o jsonpath='{.items[0].status.allocatable.nvidia\.com/gpu}'
echo
```

If the last command prints `1`, the node is advertising one GPU to Kubernetes.

### Deploy the sample on `minikube`

Use the single-node override file:

```bash
helm install vllm ./helm \
  -f values.yaml \
  -f values.minikube.yaml
```

The override does three practical things:

- switches GPU requests from `nvidia.com/mig-2g.20gb` to `nvidia.com/gpu`
- reduces the engine to one replica
- removes the `runtimeClassName` assumption that the baseline chart uses for more tuned clusters

## Step 3B: Create a GPU-enabled `k3s` cluster

This path is for a host where the cluster itself should feel more native than a Docker-driver local lab.

What the script does:

- installs `kubectl` and `helm` if needed
- disables swap for the current boot
- installs the NVIDIA Container Toolkit on Ubuntu or Debian if it is missing
- installs `k3s` with `--disable traefik --write-kubeconfig-mode 0644`
- copies kubeconfig into `~/.kube/config`
- installs or upgrades the NVIDIA GPU Operator

Run:

```bash
bash scripts/install-k3s-gpu-cluster.sh
```

### Verify `k3s`

Check node readiness and GPU Operator pods:

```bash
kubectl get nodes -o wide
kubectl get pods -n gpu-operator
kubectl get nodes -o jsonpath='{.items[0].status.allocatable.nvidia\.com/gpu}'
echo
```

You want to see the node in `Ready` state and the GPU Operator pods converging to `Running`.

### Deploy the sample on `k3s`

Use the `k3s` override file:

```bash
helm install vllm ./helm \
  -f values.yaml \
  -f values.k3s.yaml
```

That override keeps `runtimeClassName: nvidia`, switches the GPU resource to `nvidia.com/gpu`, and reduces the serving engine to one replica for a simpler single-node start.

## Step 4: Smoke-test GPU Scheduling

Before deploying vLLM, it is worth checking whether Kubernetes can schedule any GPU pod at all.

```bash
kubectl run gpu-test \
  --image=nvidia/cuda:12.4.1-base-ubuntu22.04 \
  --restart=Never \
  --limits='nvidia.com/gpu=1' \
  --command -- nvidia-smi
```

Then inspect the output:

```bash
kubectl logs gpu-test
kubectl delete pod gpu-test
```

If that pod cannot run, the sample chart will not run either.

## Step 5: Move to the vLLM Sample

Once the cluster is healthy, continue with the sample README.

Typical next commands:

```bash
helm install vllm ./helm -f values.yaml -f values.minikube.yaml
kubectl get pods
kubectl port-forward svc/vllm-router-service 30080:80
curl http://localhost:30080/v1/models
```

Or, for the `k3s` path:

```bash
helm install vllm ./helm -f values.yaml -f values.k3s.yaml
kubectl get pods
kubectl port-forward svc/vllm-router-service 30080:80
curl http://localhost:30080/v1/models
```

## Troubleshooting

### GPU not visible on the host

Symptoms:

- `nvidia-smi` fails
- Docker GPU test fails

Fix host drivers and container runtime access before touching Kubernetes.

### `minikube` was installed before NVIDIA runtime setup

Symptoms:

- cluster starts
- node exists
- GPU resource never appears

Delete the old profile and recreate it after `nvidia-ctk` has configured Docker:

```bash
minikube delete
bash scripts/install-minikube-cluster.sh
```

### `k3s` node is ready but GPU resource is missing

Symptoms:

- `kubectl get nodes` shows `Ready`
- `nvidia.com/gpu` does not appear in allocatable resources

Check:

```bash
kubectl get pods -n gpu-operator
kubectl describe node
```

If the GPU Operator pods are not healthy, fix that layer first.

### GPU Operator pods stall or crash

Check:

```bash
kubectl get pods -n gpu-operator
kubectl logs -n gpu-operator deployment/gpu-operator
```

On single-node lab clusters, GPU Operator issues are usually runtime wiring problems, host driver problems, or node resource problems rather than chart bugs.

## Conclusion

At this point, the machine should have a Kubernetes environment that can schedule GPU workloads. That is the real milestone. Once that is true, the rest of the sample becomes a Helm and values-file problem instead of a cluster bootstrap problem.
