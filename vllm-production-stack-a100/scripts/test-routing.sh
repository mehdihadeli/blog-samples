#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:30080}"
MODEL="${MODEL:-Qwen/Qwen3-0.6B}"

echo "List models"
curl -s "$BASE_URL/v1/models"
echo
echo

echo "First completion"
curl -s "$BASE_URL/v1/completions" \
  -H "Content-Type: application/json" \
  -d "{\"model\":\"$MODEL\",\"prompt\":\"You are an internal operations assistant. Summarize this runbook: step one warms the cache, step two serves the request.\",\"max_tokens\":48}"
echo
echo

echo "Second completion with shared prefix"
curl -s "$BASE_URL/v1/completions" \
  -H "Content-Type: application/json" \
  -d "{\"model\":\"$MODEL\",\"prompt\":\"You are an internal operations assistant. Summarize this runbook: step one warms the cache, step two serves the request. Then explain why repeated prefixes help.\",\"max_tokens\":48}"
echo
echo

cat <<'EOF'
Next checks:
  kubectl logs deployment/vllm-deployment-router
  kubectl logs -l model=qwen3-06b
  python client.py --base-url http://localhost:30080 prefix-demo

Sleep mode flow:
  curl -s http://localhost:30080/engines | jq
  curl -s -X POST "http://localhost:30080/sleep?id=<engine-id>" | jq
  curl -s "http://localhost:30080/is_sleeping?id=<engine-id>" | jq
  curl -s -X POST "http://localhost:30080/wake_up?id=<engine-id>" | jq

Tracing flow:
  kubectl port-forward svc/jaeger-query 16686:16686
  Open http://localhost:16686 and search for vllm-router or vllm-engine.
EOF