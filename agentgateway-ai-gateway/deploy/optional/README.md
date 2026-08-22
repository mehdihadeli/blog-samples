# Optional integrations

These files extend sample configuration without changing default local startup.

## OpenAI moderation

`moderation-policy.yaml` is a merge fragment for the two concrete DeepSeek
models. Add it to the corresponding `llm.models[]` entries and provide
`OPENAI_API_KEY` when semantic content moderation is required. Existing local
regex and builtin PII guards remain useful as a fast first layer.

Official reference:
[AgentGateway moderation documentation](https://agentgateway.dev/docs/standalone/latest/llm/prompt-guards/moderation)

## ExtMCP

## Prompt enrichment

The browser LLM route in the main configuration prepends a support-assistant
system message to `/v1/chat/completions` and `/v1/responses`. This follows the
official `ai.prompts.prepend` route-backend pattern and keeps the API-key
endpoint unchanged for service clients.

## Fault injection

`fault-injection.yaml` is a standalone gateway configuration. It exposes
`/random`, `/header`, and `/abort` on port 4100 and forwards normal traffic to
a local HTTP server on port 8080. Requests can receive synthetic delay or a
503 response. Start an upstream with `python -m http.server 8080`, then run a
second gateway process; do not mount this file over the main gateway config.

Official reference:
[Official fault-injection example](https://github.com/agentgateway/agentgateway/tree/main/examples/fault-injection)

`mcp-guardrails-policy.yaml` is a Kubernetes `AgentgatewayPolicy` for an
external ExtMCP gRPC service named `ext-mcp` on port `4445`. The policy sends
`tools/call` requests and `tools/list` responses to that service and fails
closed if the processor is unavailable.

The default sample is standalone Docker Compose, so it does not start an
ExtMCP server. Deploy an ExtMCP-compatible service and an
`AgentgatewayBackend` named `mcp-backend` before applying this fragment.

Official reference:
[AgentGateway ExtMCP setup documentation](https://agentgateway.dev/docs/kubernetes/main/mcp/guardrails/setup)
