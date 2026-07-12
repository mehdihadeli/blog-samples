# vLLM CPU Home Lab Sample

This sample runs `Qwen/Qwen3-0.6B` with the official `vllm/vllm-openai-cpu` image.

Model page: `https://huggingface.co/Qwen/Qwen3-0.6B`

It is designed for a small Linux or WSL2 host such as:

- Intel Core i5-10400
- 16 GB RAM
- no supported discrete accelerator

## What This Sample Includes

- `docker-compose.yml` for the vLLM API and Prometheus
- `docker-compose.production.yml` for an NVIDIA A100-style production baseline
- `nginx/production.conf` for edge proxying and request rate limiting in production
- `token-gateway/` for per-user token-budget enforcement in production
- `docker-compose.observability.yml` as an optional Grafana overlay
- `prometheus.yml` for scraping `/metrics`
- `alerts.yml` with starter Prometheus alert rules for this small host
- `grafana/provisioning/datasources/prometheus.yml` for automatic Grafana wiring
- `client.py` as a minimal OpenAI-compatible client

The sample now keeps its defaults directly in `docker-compose.yml`. That makes the setup easier to copy and run on a single-node home lab. If you want to tune it, edit the Compose file in place.

## Host Assumptions

Use Linux or WSL2. The current vLLM runtime path is Linux-first.

Check the host first:

```bash
docker --version
docker compose version
lscpu | grep -E 'Model name|CPU\(s\)|Flags'
free -h
```

## Bring It Up

```bash
docker compose up -d
```

## Production GPU Variant

If you want a separate production-oriented Compose file for NVIDIA A100 deployments, use:

```bash
docker compose -f docker-compose.production.yml up -d
```

That file is not meant for this small CPU host. It is a separate baseline for a GPU-backed deployment with:

- `vllm/vllm-openai`
- Nginx in front of vLLM
- request rate limiting at the edge
- a token-budget gateway behind Nginx
- Redis for per-user budget counters
- `gpus: all`
- `ipc: host`
- GPU-oriented vLLM flags such as `--gpu-memory-utilization`

Before using it, make sure the host has:

- NVIDIA drivers
- NVIDIA Container Toolkit
- one or more supported NVIDIA GPUs
- real values for `HF_TOKEN` and `VLLM_API_KEY`
- a real value for `ADMIN_API_KEY`

The production file now puts Nginx in front of vLLM and applies a basic per-IP rate limit. By default it allows `5` requests per second per client IP with a burst of `20`, plus a per-IP connection cap of `10`.

It also adds a small token-budget gateway between Nginx and vLLM. That gateway uses the caller's bearer token as the user key, estimates prompt tokens with the model tokenizer, adds requested output tokens, and enforces a per-user daily budget in Redis.

Default production budget:

- `DAILY_TOKEN_LIMIT=200000`
- `ADMIN_API_KEY=changeme-admin`

How it works:

- client sends `Authorization: Bearer user-key`
- Nginx rate-limits and forwards the request to the gateway
- the gateway reserves `prompt_tokens + max_tokens` against that user's daily budget
- if the budget is exhausted, the gateway returns `429`
- if the request is accepted, the gateway forwards it to vLLM using the internal `VLLM_API_KEY`

Admin endpoints are also available through Nginx for budget inspection and reset. They require the `X-Admin-Key` header.

Check a user's current daily usage:

```bash
curl "http://127.0.0.1:8000/admin/token-budget?user_key=user-key-1" \
  -H "X-Admin-Key: changeme-admin"
```

Check usage for a specific UTC day:

```bash
curl "http://127.0.0.1:8000/admin/token-budget?user_key=user-key-1&day=2026-07-12" \
  -H "X-Admin-Key: changeme-admin"
```

Reset a user's daily budget entry:

```bash
curl -X DELETE "http://127.0.0.1:8000/admin/token-budget?user_key=user-key-1" \
  -H "X-Admin-Key: changeme-admin"
```

## Optional: Pull the Model First with the Hugging Face CLI

Yes, you can download the model before starting vLLM.

You do not have to pre-download it. With the current Compose file, vLLM can fetch `Qwen/Qwen3-0.6B` directly on the first startup and store it in the mounted `./data/huggingface` cache.

So the simplest path is still:

```bash
docker compose up -d
```

Use the Hugging Face CLI only if you want to warm the cache in advance or keep a separate local model folder.

Install the Hugging Face CLI using the method recommended in the docs:

```bash
curl -LsSf https://hf.co/cli/install.sh | bash
hf --help
```

Alternative install method from the same docs:

```bash
pip install -U "huggingface_hub"
```

To pre-fill the same cache directory used by the Compose stack:

```bash
mkdir -p ./data/huggingface
HF_HOME=./data/huggingface hf download Qwen/Qwen3-0.6B
```

Then start the stack normally:

```bash
docker compose up -d
```

If you want the model in a regular local folder instead of the cache layout:

```bash
hf download Qwen/Qwen3-0.6B --local-dir ./models/Qwen3-0.6B
```

That second approach requires updating `docker-compose.yml` to mount `./models/Qwen3-0.6B` and changing `--model` to the mounted path.

Follow startup logs:

```bash
docker compose logs -f vllm
```

## Validate It

Health:

```bash
curl http://127.0.0.1:8000/health
```

Chat request:

```bash
curl http://127.0.0.1:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer changeme-local" \
  -d '{
    "model": "Qwen/Qwen3-0.6B",
    "messages": [
      {"role": "user", "content": "Explain Docker Compose in two sentences."}
    ],
    "max_tokens": 120,
    "temperature": 0.2
  }'
```

Metrics:

```bash
curl http://127.0.0.1:8000/metrics | head
```

Python client:

```bash
python -m venv .venv
source .venv/bin/activate
pip install openai
python client.py
```

## Optional Observability Overlay

If you want dashboards as well as metrics, start the Grafana overlay too:

```bash
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d
```

Grafana will be available on `http://127.0.0.1:3000` with the default login:

- username: `admin`
- password: `admin`

This overlay is optional on purpose. Prometheus is cheap enough for this host. Grafana is useful, but it adds more moving parts and more memory pressure.

## Compose Layout

The sample is split into a base stack and an optional overlay.

There is also a separate production file for future GPU deployments.

### Base stack

`docker-compose.yml` includes:

- the vLLM API service
- persistent Hugging Face cache
- Prometheus scraping
- alert rule loading

All of the conservative defaults for this host live directly in that file:

- model: `Qwen/Qwen3-0.6B`
- dtype: `bfloat16`
- max model length: `2048`
- max concurrent sequences: `4`
- max batched tokens: `512`
- CPU KV cache space: `4`
- reserved CPUs: `1`
- thread binding: `auto`

If you need to tune the host, change the values in `docker-compose.yml` and rerun the stack.

### Production GPU stack

`docker-compose.production.yml` is a separate starting point for NVIDIA A100-style deployments.

It replaces the CPU image and CPU tuning variables with:

- the CUDA-based `vllm/vllm-openai` image
- an Nginx edge proxy with rate limiting
- a Python token-budget proxy backed by Redis
- GPU access through Compose
- `ipc: host`
- higher concurrency and context defaults intended for GPU tuning

Treat that file as a baseline, not a final production profile. Real production values should come from load testing against your actual prompts and latency goals.

### Optional overlay

`docker-compose.observability.yml` adds Grafana without making the base stack heavier than it needs to be.

That is a better fit for a 16 GB node than forcing dashboards into the default path for every user.

## Tuning Notes

The defaults are conservative on purpose.

### Safe baseline

- `VLLM_MAX_MODEL_LEN=2048`
- `VLLM_MAX_NUM_SEQS=4`
- `VLLM_MAX_NUM_BATCHED_TOKENS=512`
- `VLLM_CPU_KVCACHE_SPACE=4`
- `VLLM_CPU_OMP_THREADS_BIND=auto`

### Tuned profile for i5-10400 after validation

Try this only after the baseline works:

```env
VLLM_MAX_MODEL_LEN=1024
VLLM_MAX_NUM_SEQS=6
VLLM_MAX_NUM_BATCHED_TOKENS=768
VLLM_CPU_KVCACHE_SPACE=3
VLLM_CPU_NUM_OF_RESERVED_CPU=1
VLLM_CPU_OMP_THREADS_BIND=0-5
```

Before hard-coding `0-5`, verify CPU topology:

```bash
lscpu -e
```

The goal is to bind one logical thread per physical core, not both hyperthreads of the same core.

## Alert Examples

The sample includes `alerts.yml` with conservative starter rules for this box.

They are not meant to be universal thresholds. They are early warning signals for the most likely failure modes on a small CPU-only node:

- queue growth
- high time to first token
- high end-to-end latency

Use them as initial guardrails, then adjust them after you have seen real traffic on your own workload.

## Security Notes

The Compose file still binds services to `127.0.0.1` and sets a local API key.

That is enough for a home-lab baseline, but not enough for broader exposure. If you want LAN or remote access later, add a reverse proxy in front and move real auth and rate limiting there.

## Why The Compose File Uses Extra Container Flags

The sample includes:

- `cap_add: SYS_NICE`
- `security_opt: seccomp=unconfined`
- `shm_size: 4g`

Those settings come from the vLLM CPU Docker guidance. They help avoid Docker-side restrictions that can reduce vLLM CPU performance or produce `get_mempolicy` warnings.

## What To Change First If It Feels Slow

1. Lower `VLLM_MAX_MODEL_LEN`
2. Lower `VLLM_MAX_NUM_SEQS`
3. Lower `VLLM_MAX_NUM_BATCHED_TOKENS`
4. Lower `VLLM_CPU_KVCACHE_SPACE`
5. Tune `VLLM_CPU_OMP_THREADS_BIND` after confirming the physical core map

## What Not To Expect

This sample is not meant for large models or real multi-user production traffic.

Use it for:

- learning vLLM
- testing OpenAI-compatible clients
- building a small internal tool
- preparing a future move to better hardware
