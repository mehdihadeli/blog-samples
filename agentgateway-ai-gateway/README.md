# AgentGateway AI Gateway Sample

A runnable reference for running [AgentGateway](https://agentgateway.dev) as an
AI gateway in front of LLM, MCP, and A2A traffic, with identity (Keycloak),
local rate limiting (in-memory token buckets), guardrails, and full
observability (OpenTelemetry + Grafana LGTM + self-hosted Langfuse).

## Architecture

```text
                          +------------------------------------------------------+
                          |                      Docker compose                   |
                          |                                                       |
  SupportChat (host)  --> |  agentgateway  --LLM 4000-->  DeepSeek API           |
   console client        |      |  --MCP 3000--> mcp-tickets/catalog/customers   |
   (MCP + LLM + A2A)     |      |  --MCP 3000--> host ToolHive proxies           |
                         |      |        (everything :19101 via ToolHive)       |
                         |      |  --A2A 3001--> support-agent (.NET A2A)        |
                         |      |  --OTLP 4317--> otel-collector                 |
                         |      |                  |---> tempo (traces)          |
                         |      |                  |---> loki  (Docker logs)     |
                         |      |                  |---> langfuse (LLM traces)   |
                         |      |  --metrics 15020--> prometheus                 |
                         |      +--> grafana (13000)                             |
                         |      +--> keycloak (8080)   - OIDC / PKCE / JWT       |
                         +------------------------------------------------------+

Rate limiting is LOCAL: the gateway holds in-memory token buckets (60 req/s +
50k tokens/h for LLM, 2,000 req/min for MCP), no external ratelimit service.
```

## What each piece does

| Piece                                                      | Role                                                                                                                                           |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `agentgateway`                                             | The gateway: API-key LLM proxy (4000), browser OIDC/PKCE LLM proxy (4001), MCP multiplexer (3000), A2A proxy (3001), Admin UI (15000).         |
| `Mcp.Tickets`, `Mcp.Catalog`, `Mcp.Customers`              | Three custom .NET MCP servers written with the MCP C# SDK (streamable HTTP at `/mcp`).                                                         |
| `mcp-everything`                                           | External stdio reference MCP server, run on the host via ToolHive and proxied to the gateway as streamable HTTP (`scripts/start-mcps.sh`).     |
| `Mcp.Time`                                                 | Compose-managed .NET MCP server exposing `get_current_time` over streamable HTTP at `mcp-time:8084/mcp`.                                       |
| `SupportAgent`                                             | A .NET A2A agent (a2a-net) hosted behind the gateway's A2A route; it answers via DeepSeek through the gateway.                                 |
| `SupportChat`                                              | A console client that talks to the LLM, MCP tools, and the A2A agent exclusively through the gateway.                                          |
| `Keycloak`                                                 | Issues JWTs; the gateway validates them for MCP (`mcpAuthentication`) and uses claims for authorization.                                       |
| `otel-collector`, `tempo`, `loki`, `prometheus`, `grafana` | LGTM observability stack: traces, logs, metrics, dashboards. The Collector `filelog` receiver reads Docker JSON logs and exports them to Loki. |
| `langfuse` + `minio`                                       | Self-hosted LLM observability; collector OTLP traces land in Langfuse, with MinIO storing Langfuse event payloads.                             |

## Feature coverage

The runnable Compose deployment covers the following features end to end:

| Feature                                                                | Sample status                                        | Main location                                |
| ---------------------------------------------------------------------- | ---------------------------------------------------- | -------------------------------------------- |
| Weighted virtual models                                                | Implemented and smoke-tested                         | `deploy/agentgateway-config.yaml`            |
| Virtual API keys and per-user labels                                   | Implemented and smoke-tested                         | `deploy/agentgateway-config.yaml`            |
| OIDC/PKCE for browser LLM access                                       | Implemented and manually verified                    | `deploy/agentgateway-config.yaml`            |
| MCP federation, streamable HTTP, Keycloak JWT, CEL authorization       | Implemented and smoke-tested                         | `deploy/agentgateway-config.yaml`            |
| A2A proxying and streaming                                             | Implemented and smoke-tested                         | `deploy/agentgateway-config.yaml`            |
| Regex and builtin PII guardrails                                       | Implemented and smoke-tested                         | `deploy/agentgateway-config.yaml`            |
| Local request limits and optional remote per-user request/token limits | Implemented and smoke-tested                         | `deploy/agentgateway-config*.yaml`           |
| Model cost catalog and `max_tokens` transformation                     | Implemented; inspect through Admin UI                | `deploy/costs/catalog.json`                  |
| OpenAPI-to-MCP Petstore target                                         | Implemented; inspect through MCP UI or MCP Inspector | `deploy/openapi/petstore.yaml`               |
| MCP retries and request mirroring                                      | Implemented; mirror sink is opt-in-safe              | `deploy/agentgateway-config*.yaml`           |
| Priority failover and health eviction                                  | Implemented; use `deepseek-resilient`                | `deploy/agentgateway-config*.yaml`           |
| OpenTelemetry, Prometheus, Grafana, Loki, Tempo, Langfuse              | Implemented and manually verified                    | `deploy/docker-compose.yaml`                 |
| Conditional policies and fault injection                               | Article pattern only                                 | See article production section               |
| Prompt enrichment                                                      | Implemented on browser LLM route                     | `deploy/agentgateway-config*.yaml`           |
| Fault injection                                                        | Optional standalone config                           | `deploy/optional/fault-injection.yaml`       |
| ExtMCP guardrails                                                      | Optional Kubernetes policy fragment                  | `deploy/optional/mcp-guardrails-policy.yaml` |
| OpenAI external moderation                                             | Optional policy fragment                             | `deploy/optional/moderation-policy.yaml`     |
| Embeddings, Responses, Messages, rerank, token-counting APIs           | Article pattern only                                 | See article production section               |
| Kubernetes catalog deployment and PostgreSQL HA                        | Article pattern only                                 | See article production section               |
| Native VS Code, GitHub Copilot, or Claude Code integration             | No first-class official recipe identified            | See article production section               |

"Article pattern only" means the article explains the feature with an
official reference and configuration shape, but this repository does not
enable or test it in the default sample. Optional policy fragments require
external credentials, a Kubernetes control plane, or a protocol-specific
service and are documented in `deploy/optional/`. This boundary keeps the
quick-start stack reproducible and prevents documentation from implying
unsupported infrastructure is already deployed.

## Prerequisites

- Docker + Docker Compose
- [ToolHive](https://github.com/stacklok/toolhive) (`winget install stacklok.thv` on Windows / `brew install thv` on macOS)
- .NET SDK 10 (only if you run the console client / build locally)
- A DeepSeek API key

## Run

```bash
cp deploy/.env.example deploy/.env   # set DEEPSEEK_API_KEY
./scripts/start-mcps.sh
```

Optional: start with per-user Envoy rate limiting (adds the ratelimit
service + Redis and switches the gateway config to its remote-ratelimit
variant):

```bash
./scripts/start-mcps.sh --ratelimit
```

The script starts the external `everything` MCP server on the host with
ToolHive, then brings up the whole compose stack (gateway, the four .NET
MCP servers, Keycloak, observability). Stop everything with
`./scripts/stop-mcps.sh`.

Then run the console client from the host:

```bash
cd src/SupportChat
dotnet run
```

The client logs in to Keycloak, lists the multiplexed MCP tools, runs three
chat turns that exercise MCP tool calls, and finally calls the A2A agent.

## ToolHive vs direct HTTP (when to use which)

MCP servers can reach the gateway two ways in this sample:

| Runtime                   | Used for                                                  | Why                                                                                                                                                                               |
| ------------------------- | --------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **ToolHive (host)**       | `mcp-everything` (third-party)                            | It is stdio-only and npx-based. `thv run` wraps it in a streamable HTTP proxy; `everything` has no Docker image at all, so ToolHive builds one on demand via its `npx://` scheme. |
| **Direct HTTP (compose)** | `mcp-tickets`, `mcp-catalog`, `mcp-customers`, `mcp-time` | These services are built from source and natively speak streamable HTTP (`app.MapMcp("/mcp")`). Compose owns their lifecycle and Docker-network connectivity.                     |

Rule of thumb:

- Third-party MCP that ships as stdio/npx/pip only → **ToolHive** (or a container plus a proxy sidecar if you must run it in-cluster).
- Your own MCP, or any MCP that natively exposes streamable HTTP → **direct HTTP in compose**.
- Trade-off to remember: ToolHive workloads are **not** managed by compose. If the terminal closes or the machine reboots, re-run `./scripts/start-mcps.sh` to bring the proxies back.

## How the gateway authenticates (which endpoint uses what)

| Endpoint                    | Auth mechanism                                                                                                                                                                                                             | Configured in                              |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| LLM gateway `:4000`         | **API key** - virtual keys (`sk-alice-*`, `sk-bob-*`); `metadata.user` feeds metrics, logs, rate limits                                                                                                                    | `llm.policies.apiKey` (mode strict)        |
| LLM browser gateway `:4001` | **OIDC Authorization Code + PKCE** through Keycloak; browser session cookie protects `/v1` requests. Separate from API-key `:4000`.                                                                                        | `routes[].policies.oidc`                   |
| MCP gateway `:3000`         | **OAuth2/OIDC JWT from Keycloak** - `mcpAuthentication` validates tokens and proxies authorization metadata/client registration; browser flows use **PKCE** via `agentgateway-browser`; `SupportChat` uses password grant. | `mcp.policies.mcpAuthentication`           |
| A2A gateway `:3001`         | **OAuth2/OIDC JWT from Keycloak** - same JWKS as MCP; browser flows use **PKCE** and the public `agentgateway-browser` client; `SupportChat` uses the password grant for demo convenience                                  | `routes[].policies.jwtAuth`                |
| Admin UI `:15000`           | None by default (local admin interface); optional OIDC policy to lock it down                                                                                                                                              | `config.adminAddr`, optional `ui.policies` |

So: `:4000` remains API-key protected, `:4001` adds browser OIDC/PKCE for LLM consumers, and MCP/A2A use Keycloak JWT/OAuth flows. Clients use one explicit authentication model per endpoint.

## Try it

| What                    | Where                                                                                                                                                                                                             |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| LLM through the gateway | `curl http://localhost:4000/v1/chat/completions -H "Authorization: Bearer sk-alice-abc123def456" -H "Content-Type: application/json" -d '{"model":"deepseek-smart","messages":[{"role":"user","content":"hi"}]}'` |
| LLM browser OIDC/PKCE   | Open `http://localhost:4001/v1/models`; it redirects to Keycloak with S256 PKCE and returns with an AgentGateway session cookie.                                                                                  |
| MCP tools list          | `curl http://localhost:3000/mcp -H "Authorization: Bearer <keycloak-token>" -X POST -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'` (see SupportChat output for a token)                                     |
| A2A agent card          | `curl http://localhost:3001/.well-known/agent-card.json -H "Authorization: Bearer <keycloak-token>"`                                                                                                              |
| Admin UI                | [http://localhost:15000/ui/](http://localhost:15000/ui/) - includes the CEL playground at `/ui/cel/` and the MCP Tool Playground                                                                                  |
| Keycloak                | [http://localhost:8080](http://localhost:8080) (admin / admin)                                                                                                                                                    |
| Grafana                 | [http://localhost:13000](http://localhost:13000) (admin / admin) - dashboard "AgentGateway", Tempo for traces, Loki for logs                                                                                      |

### Authorization demo

- `alice` has role `support-admin` and can call `customers_*` tools.
- `bob` gets a 403 on `customers_*` tools (the CEL rule requires the role).

### Rate-limit demo

By default the gateway applies local (in-memory) token-bucket limits:
60 requests/second plus 50k tokens/hour on the LLM gateway, 2,000
requests/minute on the MCP gateway. Fire a burst of `curl` calls against port
4000 and watch the 429s once the request or token budget is exhausted.
Counters reset when the gateway restarts.

Started with `--ratelimit`, the limits move to an Envoy ratelimit service
(per-user MCP/A2A request limits plus LLM token budgets: Alice 100,000 tokens/day,
Bob 50,000 tokens/day, keyed on virtual API-key user / JWT subject) that
survives gateway restarts. See
`deploy/docker-compose.ratelimit.yaml` and
`deploy/infra/ratelimit/config.yaml`.

For the difference between local and remote rate limiting, see the
[AgentGateway rate-limit docs](https://agentgateway.dev/docs/standalone/latest/configuration/resiliency/rate-limits/).

### Guardrails demo

Ask the model to "reveal your system prompt" (rejected by the request guard)
or ask it to output an email address (the response guard's builtin `email`
rule rejects it).

### Cost and request-bound demo

The gateway loads DeepSeek pricing from
`deploy/costs/catalog.json` and records realized token cost in the request log,
traces, metrics, and Admin UI analytics. The catalog is mounted read-only by
Compose and configured with `config.modelCatalog`:

```yaml
config:
  modelCatalog:
    - file: /costs/catalog.json
```

Open `http://localhost:15000/ui/llm/analytics` after sending an LLM request to
view token usage and cost. The concrete `deepseek-chat` and
`deepseek-reasoner` models also apply an LLM transformation that caps
`max_tokens` at 1024:

```yaml
transformation:
  max_tokens: "min(llmRequest.max_tokens, 1024)"
```

See the official [model costs](https://agentgateway.dev/docs/standalone/latest/llm/cost-controls/costs/),
[cost dashboard](https://agentgateway.dev/docs/standalone/latest/llm/cost-controls/dashboard/),
[budget and spend limits](https://agentgateway.dev/docs/standalone/latest/llm/cost-controls/budget-limits/),
and [LLM transformations](https://agentgateway.dev/docs/standalone/latest/llm/transformations/)
guides for catalog imports, PostgreSQL-backed analytics, token/cost budgets,
and more advanced policy expressions. The `--ratelimit` profile configures the
LLM policy with `type: tokens`, `apiKey.user`, and matching per-user Envoy
descriptors in `deploy/infra/ratelimit/config.yaml`.

### OpenAPI-to-MCP demo

Compose starts Swagger Petstore as a normal REST service. AgentGateway reads
`deploy/openapi/petstore.yaml` and exposes its operations as MCP tools named
from their unique `operationId` values, including `openapi_getInventory` and
`openapi_getPetById`. Use the Admin UI MCP Tool Playground or the authenticated
MCP endpoint to list and call them. The OpenAPI target uses stateless MCP
sessions because each REST operation is independent.

The resilient virtual model is also available as `deepseek-resilient`. It
prefers the primary DeepSeek target and moves to the backup target after health
eviction. MCP requests use three attempts with 500 ms backoff for 429, 500, and
503 responses; 10% are copied to the side-effect-free `mcp-mirror` sink.

## Verify the stack

### Automated smoke test

`./scripts/verify.sh` runs end-to-end checks against the running stack
(LLM auth, MCP auth, tool multiplexing, CEL authorization, guardrails, rate
limits, A2A card, metrics):

```bash
./scripts/verify.sh
```

It prints PASS/FAIL per check and exits non-zero if anything fails. Read the
header of the script for the mapping of each check to a gateway feature.

There is also a .NET test project using **xUnit v3** and **Shouldly**:

```bash
dotnet test tests/AgentGateway.Samples.Tests/AgentGateway.Samples.Tests.csproj
```

The tests are integration tests that call the running gateway. When the Docker
stack is down they skip with a reason, so CI can still run the project without
failing. With the stack up they cover LLM virtual-key auth, MCP multiplexing,
MCP tool-level authorization (alice vs bob), A2A JWT auth, request guardrails,
local rate limiting, and the Admin UI / metrics endpoints.

### Manual walkthrough in the Admin UI

1. Open [http://localhost:15000/ui/](http://localhost:15000/ui/) - the **Gateway Overview** lists LLM, MCP and Traffic capabilities.
2. **Traffic > Routes** - confirm three routes:
   - the LLM route on port 4000 (`llm`);
   - the MCP route on port 3000 (`/mcp`);
   - the `a2a-support-agent` route on port 3001 with the `jwtAuth` policy shown.
3. **LLM > Client Setup** - pick the `deepseek-smart` model and `sk-alice-*` key; copy a ready-to-run curl snippet and run it (validates virtual keys + the virtual model).
4. **CEL playground** at `/ui/cel/` - paste the authorization rule `'mcp.tool.target == "customers" && "support-admin" in jwt.realm_access.roles'` and inspect the request context; also try `default(jwt.sub, "anonymous")` for the rate-limit descriptor.
5. **MCP > Tool Playground** - pick a target (e.g. `tickets`), hit **Apply CORS**, then log in via Keycloak (PKCE flow with the `agentgateway-browser` client). You can now call e.g. `tickets_tickets_list` from the browser. Log in as `bob` and try `customers_customers_get` - the gateway returns 403 because of the CEL rule.
6. **MCP > connected targets** - confirm all 6 targets (tickets, catalog, customers, everything, time, and OpenAPI Petstore) are up.
7. **A2A** - the agent card endpoint (`/.well-known/agent-card.json`) on port 3001 now requires the same Keycloak JWT. Message requests use `/v1/message:send`; an unauthenticated request returns 401.
8. **Logs/Traffic** - the UI surfaces recent traffic and gateway logs; cross-check the same request IDs in Grafana (traces, logs) and Langfuse (LLM traces).

## Layout

```text
scripts/
  start-mcps.sh                 # ToolHive MCP (everything) + compose up
                                # (--ratelimit adds the Envoy override)
  stop-mcps.sh                  # inverse: stop workloads + compose down
  verify.sh                     # end-to-end smoke tests against the running stack
                                # (LLM/MCP auth, CEL authz, guardrails, 429, A2A)
deploy/
  agentgateway-config.yaml      # gateway config (llm, mcp, a2a route, tracing,
                                # local rate limits)
  agentgateway-config.remote-ratelimit.yaml   # variant with remoteRateLimit
  docker-compose.yaml           # full stack (gateway, .NET MCPs, keycloak,
                                # LGTM, langfuse)
  docker-compose.ratelimit.yaml # OPTIONAL override: Envoy ratelimit + Redis
  infra/                        # otel, tempo, loki, prometheus,
                                # grafana provisioning + dashboard, keycloak
                                # realm import, ratelimit config
src/
  AppHost/                      # Aspire AppHost for local dev without Docker
  ServiceDefaults/              # shared health, telemetry, discovery, resilience
  Mcp.Tickets|Mcp.Catalog|Mcp.Customers/   # custom .NET MCP servers
  SupportChat/                  # console client (LLM + MCP + A2A via gateway)
  SupportAgent/                 # .NET A2A agent behind the gateway
```

## Local dev without Docker

The Aspire AppHost (`src/AppHost`) runs the .NET services locally against a
locally installed gateway; see `src/AppHost/Program.cs` for the endpoints it
expects. The compose stack is the full, self-contained path.

## MCP Inspector validation

MCP Inspector is useful for testing the gateway as an MCP client, especially
the aggregated tool list and OpenAPI-generated tools. Keep the Docker Compose
stack running, obtain a Keycloak token, and run the CLI smoke test:

```bash
export KEYCLOAK_TOKEN="<alice access token>"
./scripts/inspector-smoke.sh
```

The script uses the official Inspector CLI with its HTTP transport for
Streamable HTTP. It checks
`tools/list`, calls `tickets_tickets_list`, and calls the OpenAPI-generated
`openapi_getInventory` tool through `http://localhost:3000/mcp`.

For the browser UI, run:

```bash
npx @modelcontextprotocol/inspector
```

Open the printed local Inspector URL, choose Streamable HTTP, enter
`http://localhost:3000/mcp`, and add `Authorization: Bearer <token>` as a
request header. Capture the tools list and an OpenAPI tool result as evidence.
The Admin UI at `http://localhost:15000/ui/` provides the same MCP Tool
Playground and is useful for checking CORS, OAuth/PKCE, and target health.

The sample intentionally does not add TLS/mTLS or native stdio targets to the
Docker Compose path. TLS belongs in a production deployment with managed
certificates, while native stdio is most useful when running AgentGateway as a
standalone binary. Docker Compose uses HTTP-native MCP containers and ToolHive
for the third-party stdio server.
