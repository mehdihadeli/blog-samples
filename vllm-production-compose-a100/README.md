# vLLM Production-Style Docker Compose on One A100

This sample mirrors the Kubernetes A100 stack as closely as Docker Compose allows on one host.

It keeps same main ideas, but in simpler shape:

- one vLLM engine container for one model
- one router in front of it
- one optional observability Compose file for OTel Collector, Jaeger, Prometheus, and Grafana
- one optional LMCache server profile in same vLLM Compose file
- session routing and sleep mode controlled by env vars instead of extra Compose overlays

It also has one deliberate limitation: Compose is not Kubernetes. You do not get Kubernetes API discovery, pod-level fault handling, or the full Production Stack control surface. This sample is a production-style approximation for a single host, not a replacement for the Kubernetes version.

## What is realistic in Compose

- Baseline round-robin routing through the router with static backend discovery
- Session-sticky routing with a request header
- Distributed tracing with OTel Collector and Jaeger
- Prometheus and Grafana on top of the collector
- Shared LMCache server as a separate daemon
- Sleep mode on the engines

## What is not a clean one-to-one match

- Kubernetes service discovery and router fault handling
- Helm-style rolling updates and scheduling controls
- exact parity with the Kubernetes-only operational model

For prefix-aware and KV-cache-aware routing, keep the Kubernetes sample as the main reference. The Compose router path here is intentionally conservative and uses documented static backend discovery.

## Files

- `compose.yaml`: all vLLM services in one file: engine, router, and optional cache-server profile
- `compose.observability.yaml`: tracing env plus Jaeger, OTel Collector, Prometheus, and Grafana
- `.env.example`: sample toggles for GPU selection, session routing, shared cache, sleep mode, and Grafana credentials
- `configs/otel/otel-collector-config.yaml`: collector scrape, trace, and exporter config
- `configs/prometheus/prometheus.yml`: minimal Prometheus server config for collector-pushed samples
- `configs/grafana/`: provisioning and dashboard files

## Prerequisites

- Docker Engine with Compose support
- NVIDIA Container Toolkit configured on the host
- one A100 80 GB

By default, `engine` points at GPU `0`. You do not need MIG or multiple GPUs for this sample.

If you want a different device, set `ENGINE_VISIBLE_DEVICES`.

For day-to-day use, put your local values in a `.env` file next to `compose.yaml`. The sample `.env.example` shows the supported toggles.

## 1. Start the baseline stack

```bash
docker compose up -d
docker compose ps
docker compose logs -f router
```

Expose the OpenAI-compatible API through the router on `http://localhost:30080`.

Validate it:

```bash
curl http://localhost:30080/v1/models

curl http://localhost:30080/v1/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen/Qwen3-0.6B",
    "prompt": "Explain what the router is doing in this stack.",
    "max_tokens": 64
  }'
```

## 2. Turn on session-sticky routing

Compose can mirror sticky routing cleanly by routing on a header instead of asking the router to discover Kubernetes pods.

Set these in `.env` when you want sticky routing:

```env
ROUTING_LOGIC=session
ROUTER_EXTRA_ARGS=--session-key x-session-id
```

Then start normally:

```bash
docker compose up -d
```

Send requests with the same session header:

```bash
curl http://localhost:30080/v1/completions \
  -H "Content-Type: application/json" \
  -H "x-session-id: demo-1" \
  -d '{
    "model": "Qwen/Qwen3-0.6B",
    "prompt": "Summarize router stickiness in one paragraph.",
    "max_tokens": 64
  }'
```

## 3. Turn on tracing and metrics

```bash
docker compose -f compose.yaml -f compose.observability.yaml up -d
```

Open:

- Jaeger: `http://localhost:16686`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`

Grafana default credentials:

- user: `admin`
- password: `admin`

The observability flow is the same as the Kubernetes sample:

1. router and engines emit traces to OTel Collector
2. OTel Collector exports traces to Jaeger
3. OTel Collector scrapes router and engine metrics
4. OTel Collector pushes those metrics to Prometheus through `/api/v1/write`
5. Prometheus accepts the pushed samples through its remote-write receiver
6. Grafana reads from Prometheus

## 4. Turn on shared LMCache server mode

Set these in `.env` when you want shared cache:

```env
COMPOSE_PROFILES=shared-kv
LMCACHE_REMOTE_URL=cache-server:8000
LMCACHE_REMOTE_SERDE=naive
```

Then start normally:

```bash
docker compose up -d
```

This starts the standalone `lmcache server` daemon from the same Compose file and points the engine at it through `LMCACHE_REMOTE_URL`.

## 5. Turn on sleep mode

Set these in `.env` when you want sleep mode:

```env
VLLM_SERVER_DEV_MODE=1
VLLM_EXTRA_ARGS=--enable-sleep-mode
```

Then start normally:

```bash
docker compose up -d
```

That sets `--enable-sleep-mode` on the engine and enables the development-mode env flag required for the sleep endpoints.

## 6. Compose combinations

You can combine features through env vars and one extra Compose file.

Tracing plus shared cache:

```bash
docker compose -f compose.yaml -f compose.observability.yaml up -d
```

Tracing plus sleep mode:

```bash
docker compose -f compose.yaml -f compose.observability.yaml up -d
```

## Notes on GPU pinning

The base Compose file uses `gpus: all` so Docker requests GPU support for the container, then `NVIDIA_VISIBLE_DEVICES` decides which device the process can actually see.

In this sample, the engine defaults to GPU `0`.

## Cleanup

```bash
docker compose down
docker compose -f compose.yaml -f compose.observability.yaml down
```
