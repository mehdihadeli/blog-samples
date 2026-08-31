# AgentGateway AI Gateway Sample

A runnable reference for running [AgentGateway](https://agentgateway.dev) as an
AI gateway in front of LLM, MCP, and A2A traffic, with identity (Keycloak),
local rate limiting (in-memory token buckets), guardrails, and full
observability (OpenTelemetry + Grafana LGTM + self-hosted Langfuse).

All browser and client traffic enters through YARP on `http://localhost:5000`.
YARP only forwards by public path or host. AgentGateway decides the active
authentication policy on each internal listener.

## Architecture

```mermaid
flowchart TD
  Client[Clients\nSupportChat, browser, VS Code, operators] --> Y[YARP\nlocalhost:5000]

  Y --> V1[/v1/*/]
  Y --> MCP[/mcp/*/]
  Y --> Browser[/browser/*/]
  Y --> VSCode[/vscode/*/]
  Y --> A2A[/a2a/*/]
  Y --> Admin[/admin/*/]
  Y --> Auth[/auth/*/]
  Y --> Grafana[/grafana/*/]
  Y --> Metrics[/metrics\n/otel-metrics/metrics/]
  Y --> LFHost[langfuse.localhost:5000]

  V1 --> LLM[AgentGateway :4000\nAPI-key LLM]
  MCP --> MCPL[AgentGateway :3000\nMCP multiplexer]
  Browser --> BrowserL[AgentGateway :4001\nOIDC browser LLM]
  VSCode --> VsL[AgentGateway :4002\nJWT LLM]
  A2A --> A2AL[AgentGateway :3001\nA2A JWT]
  Admin --> AdminL[AgentGateway :15000\nAdmin UI]

  LLM --> DeepSeek[DeepSeek]
  MCPL --> DotNetMcp[.NET MCP servers]
  MCPL --> ToolHive[ToolHive MCP proxies\neverything, sequentialthinking]
  MCPL --> OpenApiMcp[OpenAPI MCP target]
  A2AL --> SupportAgent[SupportAgent]

  LLM --> OTel[OpenTelemetry Collector]
  OTel --> Tempo[Tempo]
  OTel --> Loki[Loki]
  OTel --> Langfuse[Langfuse]
  Grafana --> GrafanaUi[Grafana]
```

Rate limiting is local by default: the gateway holds in-memory token buckets
for LLM and MCP traffic. The optional `docker-compose.ratelimit.yaml` overlay
switches the sample to the remote Envoy ratelimit service.

## Public gateway flow

The sample keeps stable public routes and changes authentication in
AgentGateway config instead of creating multiple edge URLs for the same
backend listener.

| Public entry                                 | Internal destination  | Active auth model                 | Notes                                                                                |
| -------------------------------------------- | --------------------- | --------------------------------- | ------------------------------------------------------------------------------------ |
| `http://localhost:5000/v1/*`                 | AgentGateway `:4000`  | API key                           | Main OpenAI-compatible LLM route.                                                    |
| `http://localhost:5000/mcp/*`                | AgentGateway `:3000`  | `mcpAuthentication` with Keycloak | Default MCP route in this sample. API key remains available as a config alternative. |
| `http://localhost:5000/browser/v1/*`         | AgentGateway `:4001`  | OIDC + PKCE                       | Browser login flow managed by AgentGateway.                                          |
| `http://localhost:5000/vscode/v1/*`          | AgentGateway `:4002`  | JWT                               | Intended for a custom VS Code provider that completes PKCE itself.                   |
| `http://localhost:5000/a2a/*`                | AgentGateway `:3001`  | JWT                               | Protects the A2A route.                                                              |
| `http://localhost:5000/auth/*`               | Keycloak `:8080`      | Keycloak UI and OIDC endpoints    | Public auth surface through YARP.                                                    |
| `http://localhost:5000/admin/*`              | AgentGateway `:15000` | None by default                   | Local operator UI.                                                                   |
| `http://localhost:5000/grafana/*`            | Grafana `:3000`       | Grafana login                     | Served through the `/grafana/` subpath.                                              |
| `http://localhost:5000/metrics`              | AgentGateway `:15020` | None                              | Gateway Prometheus metrics.                                                          |
| `http://localhost:5000/otel-metrics/metrics` | Collector `:8889`     | None                              | Collector metrics.                                                                   |
| `http://langfuse.localhost:5000/`            | Langfuse `:3000`      | Langfuse login                    | Host-based route keeps Langfuse at URL root.                                         |

### Why Langfuse uses a host-based route

Grafana is configured to work under `/grafana/`, so YARP forwards that prefix
unchanged. Langfuse is different: the stock web image expects root-relative
asset and auth paths. The sample therefore exposes Langfuse at
`http://langfuse.localhost:5000/` instead of `http://localhost:5000/langfuse/`.
This avoids rebuilding Langfuse with a custom base path.

## What each piece does

| Piece                                                      | Role                                                                                                                                                                                              |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `yarp`                                                     | The only host-facing edge at `:5000`; routes LLM, browser/VS Code, MCP, A2A, Admin, metrics, and Keycloak paths to internal services.                                                             |
| `agentgateway`                                             | Internal gateway: API-key LLM proxy (4000), browser OIDC/PKCE LLM proxy (4001), MCP multiplexer (3000), A2A proxy (3001), Admin UI (15000).                                                       |
| `Mcp.Tickets`, `Mcp.Catalog`, `Mcp.Customers`              | Three custom .NET MCP servers written with the MCP C# SDK (streamable HTTP at `/mcp`).                                                                                                            |
| `mcp-everything`                                           | External stdio reference MCP server, run on the host via ToolHive and proxied to the gateway as streamable HTTP (`scripts/start-mcps.sh`).                                                        |
| `mcp-sequentialthinking`                                   | External stdio Docker MCP server, run on the host via ToolHive and proxied to the gateway as streamable HTTP (`scripts/start-mcps.sh`).                                                           |
| `Mcp.Time`                                                 | Compose-managed .NET MCP server exposing `get_current_time` over streamable HTTP at `mcp-time:8084/mcp`.                                                                                          |
| `SupportAgent`                                             | A .NET A2A agent (a2a-net) hosted behind the gateway's A2A route; it answers via DeepSeek through the gateway.                                                                                    |
| `SupportChat`                                              | A console client that talks to the LLM, MCP tools, and the A2A agent exclusively through the gateway.                                                                                             |
| `Keycloak`                                                 | Issues JWTs; the gateway validates them for MCP (`mcpAuthentication`) and uses claims for authorization.                                                                                          |
| `otel-collector`, `tempo`, `loki`, `prometheus`, `grafana` | LGTM observability stack: traces, logs, metrics, dashboards. Grafana auto-provisions the official AgentGateway dashboard from `deployments/infra/grafana/dashboards/agentgateway-dashboard.json`. |
| `langfuse` + `minio`                                       | Self-hosted LLM observability; collector OTLP traces land in Langfuse, with MinIO storing Langfuse event payloads.                                                                                |

## Feature coverage

The runnable Compose deployment covers the following features end to end:

| Feature                                                                | Sample status                                        | Main location                                     |
| ---------------------------------------------------------------------- | ---------------------------------------------------- | ------------------------------------------------- |
| Weighted virtual models                                                | Implemented and smoke-tested                         | `deployments/agentgateway-config.yaml`            |
| Virtual API keys and per-user labels                                   | Implemented and smoke-tested                         | `deployments/agentgateway-config.yaml`            |
| OIDC/PKCE for browser LLM access                                       | Implemented and manually verified                    | `deployments/agentgateway-config.yaml`            |
| MCP federation, streamable HTTP, Keycloak JWT, CEL authorization       | Implemented and smoke-tested                         | `deployments/agentgateway-config.yaml`            |
| A2A proxying and streaming                                             | Implemented and smoke-tested                         | `deployments/agentgateway-config.yaml`            |
| Regex and builtin PII guardrails                                       | Implemented and smoke-tested                         | `deployments/agentgateway-config.yaml`            |
| Local request limits and optional remote per-user request/token limits | Implemented and smoke-tested                         | `deployments/agentgateway-config*.yaml`           |
| Model cost catalog and `max_tokens` transformation                     | Implemented; inspect through Admin UI                | `deployments/costs/catalog.json`                  |
| OpenAPI-to-MCP Petstore target                                         | Implemented; inspect through MCP UI or MCP Inspector | `deployments/openapi/petstore.yaml`               |
| MCP retries and request mirroring                                      | Implemented; mirror sink is opt-in-safe              | `deployments/agentgateway-config*.yaml`           |
| Priority failover and health eviction                                  | Implemented; use `deepseek-resilient`                | `deployments/agentgateway-config*.yaml`           |
| OpenTelemetry, Prometheus, Grafana, Loki, Tempo, Langfuse              | Implemented and manually verified                    | `deployments/docker-compose.yaml`                 |
| Conditional policies and fault injection                               | Article pattern only                                 | See article production section                    |
| Prompt enrichment                                                      | Implemented on browser LLM route                     | `deployments/agentgateway-config*.yaml`           |
| Fault injection                                                        | Optional standalone config                           | `deployments/optional/fault-injection.yaml`       |
| ExtMCP guardrails                                                      | Optional Kubernetes policy fragment                  | `deployments/optional/mcp-guardrails-policy.yaml` |
| OpenAI external moderation                                             | Optional policy fragment                             | `deployments/optional/moderation-policy.yaml`     |
| Embeddings, Responses, Messages, rerank, token-counting APIs           | Article pattern only                                 | See article production section                    |
| Kubernetes catalog deployment and PostgreSQL HA                        | Article pattern only                                 | See article production section                    |
| Native VS Code, GitHub Copilot, or Claude Code integration             | No first-class official recipe identified            | See article production section                    |

"Article pattern only" means the article explains the feature with an
official reference and configuration shape, but this repository does not
enable or test it in the default sample. Optional policy fragments require
external credentials, a Kubernetes control plane, or a protocol-specific
service and are documented in `deployments/optional/`. This boundary keeps the
quick-start stack reproducible and prevents documentation from implying
unsupported infrastructure is already deployed.

## Prerequisites

- Docker + Docker Compose
- .NET Aspire (optional, for local orchestration of .NET projects)
- [ToolHive](https://github.com/stacklok/toolhive) (`winget install stacklok.thv` on Windows / `brew install thv` on macOS)
- .NET SDK 10 (only if you run the console client / build locally)
- A DeepSeek API key
- `curl`, `python` on `PATH`, and optionally `jq` for `scripts/verify.sh`

## Run

```bash
cp deployments/.env.example deployments/.env   # set DEEPSEEK_API_KEY
./scripts/start-mcps.sh
docker compose -f deployments/docker-compose.yaml up -d --build
```

For local .NET development, run the Aspire AppHost instead. It orchestrates
the four first-party MCP projects, SupportAgent, and SupportChat as processes
with service health and the Aspire dashboard. Infrastructure and AgentGateway
remain available through the Compose deployment above.

```bash
dotnet run --project src/AppHost/AppHost.csproj
```

Use Compose when you need the complete containerized deployment. Do not run
the Compose MCP services and the Aspire MCP projects on the same host ports at
the same time.

Optional: add per-user Envoy rate limiting (adds the ratelimit service + Redis
and switches the gateway config to its remote-ratelimit variant):

```bash
docker compose -f deployments/docker-compose.yaml \
  -f deployments/docker-compose.ratelimit.yaml up -d --build
```

The script starts the external `everything` and `sequentialthinking` MCP
servers on the host with ToolHive. Docker Compose manages the gateway, the
four .NET MCP servers, Keycloak, and observability. Stop the MCP proxies with
`./scripts/stop-mcps.sh`; stop the Compose stack manually with the matching
`docker compose ... down` command.

Then run the console client from the host:

```bash
cd src/SupportChat
dotnet run
```

The client logs in to Keycloak, lists the multiplexed MCP tools, runs three
chat turns that exercise MCP tool calls, and finally calls the A2A agent.

## Testing

Run tests only after external ToolHive MCP proxies and the Compose stack are
running. First configure both values in `deployments/.env`:

```dotenv
DEEPSEEK_API_KEY=your-deepseek-api-key
DEEPSEEK_ENDPOINT=api.deepseek.com:443
```

From `samples/agentgateway-ai-gateway`, start the external ToolHive MCP
proxies first, then start the Compose stack:

```bash
./scripts/start-mcps.sh
docker compose -f deployments/docker-compose.yaml up -d --build
```

On Windows, the simplest validated path is Git Bash with the host Python
installation available there. If running tests from PowerShell, also load
provider settings into the current process because
`dotnet test` does not automatically read `deployments/.env`:

```powershell
$env:DEEPSEEK_API_KEY = (Get-Content deployments/.env | Where-Object { $_ -match '^DEEPSEEK_API_KEY=' }) -replace '^DEEPSEEK_API_KEY=', ''
$env:DEEPSEEK_ENDPOINT = (Get-Content deployments/.env | Where-Object { $_ -match '^DEEPSEEK_ENDPOINT=' }) -replace '^DEEPSEEK_ENDPOINT=', ''
```

Run the test project from the repository root:

```bash
dotnet test tests/AgentGateway.Samples.Tests/AgentGateway.Samples.Tests.csproj
```

See [`tests/README.md`](tests/README.md) for the complete setup, startup, and
cleanup sequence.

The test project expects AgentGateway on its configured host ports and uses
the ToolHive proxies for the `everything` and `sequentialthinking` targets.
Tests that call the real DeepSeek provider are skipped when
`DEEPSEEK_API_KEY` is missing. Stop the external proxies with
`./scripts/stop-mcps.sh` and stop Compose with
`docker compose -f deployments/docker-compose.yaml down`.

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

## Security approaches and authentication

### API keys first: LLM and MCP

API keys are the simplest approach for clients that can securely store and
send a bearer secret. AgentGateway supports strict `apiKey` authentication for
both LLM and MCP configuration. The sample enables it for the LLM endpoint on
`:4000`:

```yaml
llm:
  policies:
    apiKey:
      mode: strict
      keys:
        - key: $ALICE_GATEWAY_KEY
          metadata:
            name: alice
            user: alice
```

The same policy can protect `/mcp` when an MCP client supports API keys:

```yaml
mcp:
  policies:
    apiKey:
      mode: strict
      keys:
        - key: $ALICE_GATEWAY_KEY
          metadata:
            name: alice
            user: alice
```

This MCP API-key policy is documented as an option but is not enabled in this
sample. The running `/mcp` endpoint uses Keycloak OAuth and JWT validation so
the MCP Tool Playground can complete PKCE.

For each user, team, or application, open **LLM > Virtual API Keys** in the
AgentGateway Admin UI, select **New key**, enter a name such as `alice` or
`customer-acme-app`, generate the key, and add metadata such as `user` and
`tenant`. Store the generated secret in the consumer's secret manager; the UI
masks it after creation. Create separate keys so each one can have its own
allowed models, tool access, rate limit, budget, expiry, rotation, and
revocation policy.

![AgentGateway Virtual API Keys showing named consumer keys](/docs/agentgateway-virtual-api-keys.png)

For many consumers, use AgentGateway `hybrid` configuration with persistent
PostgreSQL storage. Keep stable routes and provider settings in YAML, and let
the Admin UI or config API manage dynamic key resources. Protect the Admin UI
as an operator surface. A separate authenticated provisioning service should
own customer self-service, lifecycle, and audit workflows.

API keys fit backend workers, CI jobs, and VS Code's built-in Custom Endpoint
provider. They are bearer credentials, not proof of stronger security by
themselves. Use HTTPS, secret storage, strict mode, narrow permissions,
rotation, expiration, and revocation. Never send `DEEPSEEK_API_KEY` to a
consumer.

### OAuth 2.0 PKCE when client supports browser login

OAuth Authorization Code with PKCE fits browser-based and interactive clients.
The client opens the identity-provider login, uses the code verifier to stop
authorization-code interception, stores short-lived tokens, and sends the JWT
to the gateway. AgentGateway uses `oidc` for browser LLM routes and
`mcpAuthentication` for MCP OAuth discovery and JWT validation. VS Code's MCP
integration can use this flow when the MCP server advertises OAuth metadata;
its built-in Custom Endpoint LLM flow uses an API key. A custom VS Code LLM
provider extension is needed for PKCE-based LLM access.

### How the gateway authenticates (which endpoint uses what)

| Endpoint                    | Auth mechanism                                                                                                                                                                                                        | Configured in                                             |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| LLM gateway `:4000`         | **API key** - virtual keys (`sk-alice-*`, `sk-bob-*`); `metadata.user` feeds metrics, logs, rate limits                                                                                                               | `llm.policies.apiKey` (mode strict)                       |
| LLM browser gateway `:4001` | **OIDC Authorization Code + PKCE** through Keycloak; browser session cookie protects `/v1` requests. Separate from API-key `:4000`.                                                                                   | `routes[].policies.oidc`                                  |
| MCP gateway `:3000`         | **API key or OAuth2/OIDC JWT** - strict `apiKey` is available for clients that support bearer keys; this sample uses `mcpAuthentication` with Keycloak, PKCE browser discovery, and password grant for `SupportChat`. | `mcp.policies.apiKey` or `mcp.policies.mcpAuthentication` |
| A2A gateway `:3001`         | **OAuth2/OIDC JWT from Keycloak** - same JWKS as MCP; browser flows use **PKCE** and the public `agentgateway-browser` client; `SupportChat` uses the password grant for demo convenience                             | `routes[].policies.jwtAuth`                               |
| Admin UI `:15000`           | None by default (local admin interface); optional OIDC policy to lock it down                                                                                                                                         | `config.adminAddr`, optional `ui.policies`                |

So: `:4000` uses API keys, `:4001` adds browser OIDC/PKCE for LLM consumers, and `/mcp` uses Keycloak JWT/OAuth in this sample while also supporting an API-key alternative. Clients use one explicit authentication model per endpoint.

## Try it

| What                    | Where                                                                                                                                                                                                             |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| LLM through the gateway | `curl http://localhost:5000/v1/chat/completions -H "Authorization: Bearer sk-alice-abc123def456" -H "Content-Type: application/json" -d '{"model":"deepseek-smart","messages":[{"role":"user","content":"hi"}]}'` |
| LLM browser OIDC/PKCE   | Open `http://localhost:5000/browser/v1/models`; it redirects through `http://localhost:5000/auth` with S256 PKCE.                                                                                                 |
| MCP tools list          | `curl http://localhost:5000/mcp -H "Authorization: Bearer <keycloak-token>" -X POST -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'` (see SupportChat output for a token)                                     |
| A2A agent card          | `curl http://localhost:5000/a2a/.well-known/agent-card.json -H "Authorization: Bearer <keycloak-token>"`                                                                                                          |
| Admin UI                | [http://localhost:5000/admin/ui/](http://localhost:5000/admin/ui/) - includes the CEL playground and MCP Tool Playground                                                                                          |
| Keycloak                | [http://localhost:5000/auth](http://localhost:5000/auth) (admin / admin)                                                                                                                                          |
| Grafana                 | [http://localhost:5000/grafana/](http://localhost:5000/grafana/) (admin / admin), routed by YARP to internal Grafana.                                                                                             |
| Langfuse                | [http://langfuse.localhost:5000/](http://langfuse.localhost:5000/) - self-hosted LLM trace UI exposed through YARP with a dedicated host route.                                                                   |

### Curl recipes

Use these commands after the ToolHive proxies and Compose stack are up.

#### 1. Health checks

```bash
curl http://localhost:5000/metrics
curl http://localhost:5000/otel-metrics/metrics
curl http://langfuse.localhost:5000/api/public/health
```

#### 2. LLM request with gateway API key

```bash
curl http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer sk-alice-abc123def456" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "deepseek-smart",
    "messages": [
      { "role": "user", "content": "List available support tools in one sentence." }
    ]
  }'
```

#### 3. Get a Keycloak access token for MCP and A2A

Demo users come from the imported realm. `alice` has the `support-admin`
role; `bob` does not.

```bash
curl -X POST http://localhost:5000/auth/realms/agentgateway/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=supportchat" \
  -d "username=alice" \
  -d "password=alice-password"
```

With `jq`:

```bash
export KEYCLOAK_TOKEN=$(curl -s -X POST http://localhost:5000/auth/realms/agentgateway/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=supportchat" \
  -d "username=alice" \
  -d "password=alice-password" | jq -r .access_token)
```

Without `jq`, copy the `access_token` value from the JSON response into an
environment variable manually.

#### 4. List aggregated MCP tools

```bash
curl http://localhost:5000/mcp \
  -H "Authorization: Bearer $KEYCLOAK_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list"
  }'
```

#### 5. Call a first-party MCP tool

```bash
curl http://localhost:5000/mcp \
  -H "Authorization: Bearer $KEYCLOAK_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "tickets_tickets_list",
      "arguments": {}
    }
  }'
```

#### 6. Call an OpenAPI-generated MCP tool

```bash
curl http://localhost:5000/mcp \
  -H "Authorization: Bearer $KEYCLOAK_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "openapi_getInventory",
      "arguments": {}
    }
  }'
```

#### 7. Prove MCP authorization is enforced

Get a token for `bob`, then call a customer tool. The request should fail with
403 because the CEL policy requires the `support-admin` role.

```bash
export BOB_TOKEN=$(curl -s -X POST http://localhost:5000/auth/realms/agentgateway/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=supportchat" \
  -d "username=bob" \
  -d "password=bob-password" | jq -r .access_token)

curl -i http://localhost:5000/mcp \
  -H "Authorization: Bearer $BOB_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 4,
    "method": "tools/call",
    "params": {
      "name": "customers_customers_get",
      "arguments": { "id": 1 }
    }
  }'
```

#### 8. Read the A2A agent card

```bash
curl http://localhost:5000/a2a/.well-known/agent-card.json \
  -H "Authorization: Bearer $KEYCLOAK_TOKEN"
```

#### 9. Trigger local rate limiting

This sends many small requests through the LLM route. Some responses should
eventually become `429 Too Many Requests`.

```bash
for i in $(seq 1 80); do \
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/v1/models \
    -H "Authorization: Bearer sk-alice-abc123def456"; \
done
```

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
`deployments/docker-compose.ratelimit.yaml` and
`deployments/infra/ratelimit/config.yaml`.

For the difference between local and remote rate limiting, see the
[AgentGateway rate-limit docs](https://agentgateway.dev/docs/standalone/latest/configuration/resiliency/rate-limits/).

### Guardrails demo

Ask the model to "reveal your system prompt" (rejected by the request guard)
or ask it to output an email address (the response guard's builtin `email`
rule rejects it).

### Cost and request-bound demo

The gateway loads DeepSeek pricing from
`deployments/costs/catalog.json` and records realized token cost in the request log,
traces, metrics, and Admin UI analytics. The catalog is mounted read-only by
Compose and configured with `config.modelCatalog`:

```yaml
config:
  modelCatalog:
    - file: /costs/catalog.json
```

Open `http://localhost:5000/admin/ui/llm/analytics` after sending an LLM request to
view token usage and cost. The concrete `deepseek-v4-flash` and
`deepseek-v4-pro` models also apply an LLM transformation that caps
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
descriptors in `deployments/infra/ratelimit/config.yaml`.

### OpenAPI-to-MCP demo

Compose starts Swagger Petstore as a normal REST service. AgentGateway reads
`deployments/openapi/petstore.yaml` and exposes its operations as MCP tools named
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

`./scripts/verify.sh` is the fast operational smoke test for a running stack.
It checks LLM and MCP authentication, tool multiplexing, CEL authorization,
guardrails, rate limits, the A2A card, and metrics:

```bash
./scripts/verify.sh
```

It prints PASS/FAIL per check and exits non-zero if anything fails. Read the
header of the script for the mapping of each check to a gateway feature.

The C# suite provides deeper integration coverage using **xUnit v3** and
**Shouldly**. It is the right place for typed assertions and provider-backed
LLM checks, while `verify.sh` remains a dependency-light health check:

```bash
dotnet test tests/AgentGateway.Samples.Tests/AgentGateway.Samples.Tests.csproj
```

The tests call the running gateway. When the Docker stack is down they skip
with a reason, so CI can still run the project without failing. With the stack
up they cover LLM virtual-key auth, MCP multiplexing, MCP tool-level
authorization (alice vs bob), A2A JWT auth, request guardrails, local rate
limiting, and the Admin UI / metrics endpoints.

The concrete DeepSeek route theory calls each configured provider model. Load
the key from `deployments/.env` into the current process, then run the LLM test
module directly with the xUnit v3 runner:

```powershell
$env:DEEPSEEK_API_KEY = (Get-Content deployments/.env | Where-Object { $_ -match '^DEEPSEEK_API_KEY=' } | ForEach-Object { $_.Substring('DEEPSEEK_API_KEY='.Length) })
dotnet exec tests/AgentGateway.Samples.Tests/bin/Debug/net10.0/AgentGateway.Samples.Tests.dll --filter-class AgentGateway.Samples.Tests.LlmGatewayTests
```

This validates `deepseek-v4-flash`, `deepseek-v4-pro`, and
`deepseek-v4-flash-vision-exp` through AgentGateway. The provider key is read
by the test process and is never printed or used as the gateway client key.

### Manual walkthrough in the Admin UI

1. Open [http://localhost:5000/admin/ui/](http://localhost:5000/admin/ui/) - the **Gateway Overview** lists LLM, MCP and Traffic capabilities.
2. **Traffic > Routes** - confirm three routes:
   - the LLM route on port 4000 (`llm`);
   - the MCP route on port 3000 (`/mcp`);
   - the `a2a-support-agent` route on port 3001 with the `jwtAuth` policy shown.
3. **LLM > Client Setup** - pick the `deepseek-smart` model and `sk-alice-*` key; copy a ready-to-run curl snippet and run it (validates virtual keys + the virtual model).
4. **CEL playground** at `/ui/cel/` - paste the authorization rule `'mcp.tool.target == "customers" && "support-admin" in jwt.realm_access.roles'` and inspect the request context; also try `default(jwt.sub, "anonymous")` for the rate-limit descriptor.
5. **MCP > Tool Playground** - pick a target (e.g. `tickets`), hit **Apply CORS**, then log in via Keycloak (PKCE flow with the `agentgateway-browser` client). You can now call e.g. `tickets_tickets_list` from the browser. Log in as `bob` and try `customers_customers_get` - the gateway returns 403 because of the CEL rule.
6. **MCP > connected targets** - confirm all 7 targets (tickets, catalog, customers, everything, sequentialthinking, time, and OpenAPI Petstore) are up.
7. **A2A** - the agent card endpoint (`/.well-known/agent-card.json`) on port 3001 now requires the same Keycloak JWT. Message requests use `/v1/message:send`; an unauthenticated request returns 401.
8. **Logs/Traffic** - the UI surfaces recent traffic and gateway logs; cross-check the same request IDs in Grafana (traces, logs) and Langfuse (LLM traces).

## Layout

```text
scripts/
  start-mcps.sh                 # start ToolHive MCPs (everything, sequentialthinking)
  stop-mcps.sh                  # stop ToolHive MCPs (everything, sequentialthinking)
  verify.sh                     # end-to-end smoke tests against the running stack
                                # (LLM/MCP auth, CEL authz, guardrails, 429, A2A)
deployments/
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
`openapi_getInventory` tool through `http://localhost:5000/mcp`.

For the browser UI, run:

```bash
npx @modelcontextprotocol/inspector
```

Open the printed local Inspector URL, choose Streamable HTTP, enter
`http://localhost:5000/mcp`, and add `Authorization: Bearer <token>` as a
request header. Capture the tools list and an OpenAPI tool result as evidence.
The Admin UI at `http://localhost:5000/admin/ui/` provides the same MCP Tool
Playground and is useful for checking CORS, OAuth/PKCE, and target health.

The sample intentionally does not add TLS/mTLS or native stdio targets to the
Docker Compose path. TLS belongs in a production deployment with managed
certificates, while native stdio is most useful when running AgentGateway as a
standalone binary. Docker Compose uses HTTP-native MCP containers and ToolHive
for the third-party stdio server.
