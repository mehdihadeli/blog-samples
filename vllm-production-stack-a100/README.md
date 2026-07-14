# vLLM Production Stack on One A100

This sample keeps the Kubernetes version closer to the simplified Docker Compose sample without pretending the two platforms have the same operational cost.

You still get the production-shaped parts that matter here: router, engine replicas when the cluster can support them, LMCache-aware options, OTel-based metrics collection, Prometheus, and Grafana. The sample now also includes local bootstrap scripts for the two practical single-node paths in this repo: `minikube` and `k3s`.

If you want the full cluster bootstrap flow before touching Helm, read [INSTALL-KUBERNETES.md](INSTALL-KUBERNETES.md).

## What this sample covers

- one main values file for vLLM-side settings
- one observability values file for Jaeger, OTel Collector, Prometheus, and Grafana
- one `minikube` override file for single-node Docker-driver GPU setups
- one `k3s` override file for single-node containerd plus GPU Operator setups
- bootstrap scripts for `kubectl`, `helm`, `minikube`, and `k3s`
- prefix-aware or KV-aware routing by editing the main values file
- shared KV cache and sleep mode by editing the main values file

## Prerequisites

- Linux host with NVIDIA driver installed
- Docker working with GPU access if you plan to use `minikube`
- sudo access on the host
- an NVIDIA A100 80 GB

The baseline `values.yaml` still assumes a more tuned cluster shape:

- `runtimeClassName: nvidia`
- two serving replicas
- one MIG slice per replica via `requestGPUType: nvidia.com/mig-2g.20gb`

If your cluster does not expose MIG slices, use one of the single-node override files in this sample instead of applying `values.yaml` by itself.

## Layout

- `values.yaml`: main vLLM-side settings for router, engines, optional cache-server, and feature toggles
- `values.observability.yaml`: router and engine OpenTelemetry settings plus embedded Jaeger, OTel Collector, Prometheus, and Grafana resources
- `values.minikube.yaml`: one-replica override for `minikube` on a single GPU without MIG
- `values.k3s.yaml`: one-replica override for `k3s` with `runtimeClassName: nvidia`
- `scripts/install-kubectl.sh`: installs `kubectl` into `~/.local/bin`
- `scripts/install-helm.sh`: installs `helm`
- `scripts/install-minikube-cluster.sh`: installs and starts a GPU-enabled `minikube` profile with the Docker driver
- `scripts/install-k3s-gpu-cluster.sh`: installs `k3s`, installs the NVIDIA Container Toolkit when needed, and deploys the GPU Operator
- `helm/`: self-contained chart for this sample
- `helm/configs/`: separate OTel, Prometheus, and Grafana config payloads consumed by Helm-managed ConfigMaps
- `scripts/render-manifests.sh`: render chart outputs into `generated-manifests/`
- `scripts/render-manifests.ps1`: PowerShell render helper for Windows shells
- `client.py`: OpenAI-compatible smoke test client
- `scripts/test-routing.sh`: repeated-prefix routing test

## 1. Bootstrap the Kubernetes environment

The short version is below. The full step-by-step guide lives in [INSTALL-KUBERNETES.md](INSTALL-KUBERNETES.md).

This repo now supports two cluster bootstrap paths.

Use `minikube` when you want the fastest local cluster on a Linux box that already runs Docker with GPU support.

Use `k3s` when the same GPU server will stay up and behave more like a small real node than a disposable dev cluster.

### Option A: Minikube with GPU support

The script follows the same general shape as the upstream Production Stack utilities:

- install `kubectl`
- install `helm`
- install `minikube` when missing
- configure Docker through `nvidia-ctk`
- start a Docker-driver cluster with `--gpus all`

Run:

```bash
bash scripts/install-minikube-cluster.sh
```

Useful environment overrides:

```bash
MINIKUBE_MEMORY=40960 MINIKUBE_CPUS=16 bash scripts/install-minikube-cluster.sh
```

Then verify the node advertises a GPU resource:

```bash
kubectl get nodes -o jsonpath='{.items[0].status.allocatable.nvidia\.com/gpu}'
echo
```

Deploy this sample with the override file that removes the MIG assumption:

```bash
helm install vllm ./helm \
  -f values.yaml \
  -f values.minikube.yaml
```

### Option B: K3s with GPU support

The `k3s` path is aimed at Ubuntu or Debian-class hosts where you want a lightweight long-running cluster on the same box as the GPU.

The script does four main things:

- install `kubectl` and `helm`
- disable swap for the current boot
- install the NVIDIA Container Toolkit when it is missing
- install or restart `k3s`, then deploy the NVIDIA GPU Operator

Run:

```bash
bash scripts/install-k3s-gpu-cluster.sh
```

The script copies the kubeconfig into `~/.kube/config`. After it finishes, verify the node and GPU Operator pods:

```bash
kubectl get nodes -o wide
kubectl get pods -n gpu-operator
kubectl get nodes -o jsonpath='{.items[0].status.allocatable.nvidia\.com/gpu}'
echo
```

Deploy this sample with the `k3s` override file:

```bash
helm install vllm ./helm \
  -f values.yaml \
  -f values.k3s.yaml
```

## 2. Deploy baseline serving stack

If you already have a tuned cluster with MIG slices exposed as `nvidia.com/mig-2g.20gb`, apply the baseline values as-is:

```bash
helm install vllm ./helm \
  -f values.yaml

kubectl get pods
kubectl get svc
kubectl port-forward svc/vllm-router-service 30080:80
```

If you used one of the bootstrap scripts above, keep the matching override file on the Helm command line.

Smoke-test the OpenAI-compatible API:

```bash
curl http://localhost:30080/v1/models

curl http://localhost:30080/v1/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen/Qwen3-0.6B",
    "prompt": "Explain what the production-stack router does.",
    "max_tokens": 64
  }'
```

Or use the Python client:

```bash
python client.py
```

## 3. Turn on prefix-aware routing

Edit `values.yaml` and set these fields:

- `routerSpec.routingLogic: prefixaware`
- `routerSpec.prefixMinMatchLength: 256`

Then apply the same files again:

```bash
helm upgrade vllm ./helm \
  -f values.yaml \
  -f values.minikube.yaml
```

For `k3s`, swap `values.minikube.yaml` for `values.k3s.yaml`. If you are on a tuned MIG setup, omit the override file.

Run the routing test and inspect logs:

```bash
./scripts/test-routing.sh
kubectl logs deployment/vllm-deployment-router
kubectl logs -l model=qwen3-06b
```

## 4. Turn on KV-cache-aware routing

Edit `values.yaml` and make these changes:

- set `routerSpec.routingLogic: kvaware`
- uncomment `routerSpec.lmcacheControllerPort`, `routerSpec.lmcacheControllerReplyPort`, and `routerSpec.lmcacheControllerHeartbeatPort`
- set `servingEngineSpec.modelSpec[0].lmcacheConfig.enableController: true`
- optionally add `VLLM_LOGGING_LEVEL=DEBUG` under the engine env list

Then apply the same files again:

```bash
helm upgrade vllm ./helm \
  -f values.yaml \
  -f values.k3s.yaml
```

Run the same repeated-prefix test and inspect logs again:

```bash
./scripts/test-routing.sh
kubectl logs deployment/vllm-deployment-router
kubectl logs -l model=qwen3-06b
```

## 5. Turn on tracing and OTel-based metrics collection

Upgrade the vLLM release with the observability values file:

```bash
helm upgrade vllm ./helm \
  -f values.yaml \
  -f values.k3s.yaml \
  -f values.observability.yaml
```

For `minikube`, swap `values.k3s.yaml` for `values.minikube.yaml`.

This renders Jaeger, the OTel Collector, Prometheus, and Grafana inside the same Helm release.

What this wiring does:

- router spans go to `otel-collector:4317`
- engine spans go to `otel-collector:4317`
- OTel Collector exports traces to Jaeger
- OTel Collector scrapes `/metrics` from router and engine pods
- OTel Collector pushes those metrics to Prometheus through `/api/v1/write`
- Prometheus accepts those samples through its remote-write receiver

Forward the trace UI:

```bash
kubectl port-forward svc/jaeger-query 16686:16686
```

Then send a request and inspect traces in `http://localhost:16686`.

## 6. Use Prometheus and Grafana from the same Helm release

Prometheus and Grafana now come from the same observability values file.

Forward both UIs:

```bash
kubectl port-forward svc/prometheus 9090:9090
kubectl port-forward svc/grafana 3000:3000
```

Open:

- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`

Grafana sample credentials:

- user: `admin`
- password: `admin`

## 7. Turn on shared KV cache

Edit `values.yaml` and set `cacheserverSpec.enabled: true`, then apply it again:

```bash
helm upgrade vllm ./helm \
  -f values.yaml \
  -f values.k3s.yaml
```

That starts an LMCache cache-server deployment inside the same chart instead of sending you to another overlay file.

## 8. Turn on sleep mode

Edit `values.yaml` and make these changes:

- set `VLLM_SERVER_DEV_MODE` to `1`
- add `--enable-sleep-mode` to `vllmConfig.extraArgs`

Then apply the same files again:

```bash
helm upgrade vllm ./helm \
  -f values.yaml \
  -f values.k3s.yaml
```

Check engines and sleep state:

```bash
curl -s http://localhost:30080/engines | jq
curl -s -X POST "http://localhost:30080/sleep?id=<engine-id>" | jq
curl -s "http://localhost:30080/is_sleeping?id=<engine-id>" | jq
curl -s -X POST "http://localhost:30080/wake_up?id=<engine-id>" | jq
```

## 9. Validation notes

If your terminal environment is healthy, these are the first commands to run:

```bash
./scripts/render-manifests.sh

helm template vllm ./helm -f values.yaml
helm template vllm ./helm -f values.yaml -f values.minikube.yaml
helm template vllm ./helm -f values.yaml -f values.k3s.yaml
helm template vllm ./helm -f values.yaml -f values.k3s.yaml -f values.observability.yaml
```

On Windows PowerShell, use:

```powershell
.\scripts\render-manifests.ps1

helm template vllm .\helm -f .\values.yaml
helm template vllm .\helm -f .\values.yaml -f .\values.minikube.yaml
helm template vllm .\helm -f .\values.yaml -f .\values.k3s.yaml
helm template vllm .\helm -f .\values.yaml -f .\values.k3s.yaml -f .\values.observability.yaml
```

The script writes rendered files into `generated-manifests/` by default. Pass a custom output directory as the first argument if needed.

Prometheus receiver checks after deployment:

```bash
kubectl port-forward svc/prometheus 9090:9090
curl -s http://localhost:9090/api/v1/status/flags
```

Useful Prometheus spot checks:

```promql
sum(vllm:num_requests_running)
100 * avg(vllm:kv_cache_usage_perc)
histogram_quantile(0.95, sum(rate(vllm:time_to_first_token_seconds_bucket[5m])) by (le))
sum(rate(vllm:generation_tokens_total[5m]))
```

## Cleanup

```bash
helm uninstall vllm
```
