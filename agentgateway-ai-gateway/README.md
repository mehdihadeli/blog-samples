# AgentGateway AI Gateway Sample

A runnable reference for running [AgentGateway](https://agentgateway.dev) as an
AI gateway in front of LLM, MCP, and A2A traffic, with identity (Keycloak),
local rate limiting (in-memory token buckets), guardrails, and full
observability (OpenTelemetry + Grafana LGTM + self-hosted Langfuse).

## Architecture

```
                          +------------------------------------------------------+
                          |                      Docker compose                   |
                          |                                                       |
  SupportChat (host)  --> |  agentgateway  --LLM 4000-->  DeepSeek API           |
   console client        |      |  --MCP 3000--> mcp-tickets/catalog/customers   |
   (MCP + LLM + A2A)     |      |  --MCP 3000--> host ToolHive proxies           |
                         |      |        (everything :19101, time :19102)        |
                         |      |  --A2A 3001--> support-agent (.NET A2A)        |
                         |      |  --OTLP 4317--> otel-collector                 |
                         |      |                  |---> tempo (traces)          |
                         |      |                  |---> loki  (logs, via        |
                         |      |                  |      promtail)              |
                         |      |                  |---> langfuse (LLM traces)   |
                         |      |  --metrics 15020--> prometheus                 |
                         |      +--> grafana (13000)                             |
                         |      +--> keycloak (8080)   - OIDC / PKCE / JWT       |
                         +------------------------------------------------------+

Rate limiting is LOCAL: the gateway holds in-memory token buckets (60 req/s +
50k tokens/h for LLM, 100 req/min for MCP), no external ratelimit service.
```

## What each piece does

| Piece                                                                  | Role                                                                                                                                                                                  |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `agentgateway`                                                         | The gateway: LLM proxy (4000), MCP multiplexer (3000), A2A proxy (3001), Admin UI (15000).                                                                                            |
| `Mcp.Tickets`, `Mcp.Catalog`, `Mcp.Customers`                          | Three custom .NET MCP servers written with the MCP C# SDK (streamable HTTP at `/mcp`).                                                                                                |
| `mcp-everything`, `mcp-time`                                           | Two external MCP servers (reference servers from modelcontextprotocol/servers), run on the host via ToolHive and proxied to the gateway as streamable HTTP (`scripts/start-mcps.sh`). |
| `SupportAgent`                                                         | A .NET A2A agent (a2a-net) hosted behind the gateway's A2A route; it answers via DeepSeek through the gateway.                                                                        |
| `SupportChat`                                                          | A console client that talks to the LLM, MCP tools, and the A2A agent exclusively through the gateway.                                                                                 |
| `Keycloak`                                                             | Issues JWTs; the gateway validates them for MCP (`mcpAuthentication`) and uses claims for authorization.                                                                              |
| `otel-collector`, `tempo`, `loki`, `promtail`, `prometheus`, `grafana` | LGTM observability stack: traces, logs, metrics, dashboards.                                                                                                                          |
| `langfuse`                                                             | Self-hosted LLM observability, fed through the collector's OTLP exporter.                                                                                                             |

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

The script starts the two external MCP servers (everything, time) on the host
with ToolHive, then brings up the whole compose stack (gateway, the three .NET
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

| Runtime                   | Used for                                      | Why                                                                                                                                                                                                                                                        |
| ------------------------- | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **ToolHive (host)**       | `mcp-everything`, `mcp-time` (third-party)    | Both are stdio-only by default. `thv run` wraps them in a streamable HTTP proxy on the host (`--transport stdio --proxy-mode streamable-http`). `everything` has no docker image at all (npx-only), so `thv` builds one on demand via its `npx://` scheme. |
| **Direct HTTP (compose)** | `mcp-tickets`, `mcp-catalog`, `mcp-customers` | They are YOUR services, built from source, and natively speak streamable HTTP (`app.MapMcp("/mcp")`). Nothing to proxy; compose gives them restart policy, the gateway's Docker network, and lifecycle tied to the stack.                                  |

Rule of thumb:

- Third-party MCP that ships as stdio/npx/pip only → **ToolHive** (or a container plus a proxy sidecar if you must run it in-cluster).
- Your own MCP, or any MCP that natively exposes streamable HTTP → **direct HTTP in compose**.
- Trade-off to remember: ToolHive workloads are **not** managed by compose. If the terminal closes or the machine reboots, re-run `./scripts/start-mcps.sh` to bring the proxies back.

## How the gateway authenticates (which endpoint uses what)

| Endpoint            | Auth mechanism                                                                                                                                                                                                                                                                      | Configured in                              |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| LLM gateway `:4000` | **API key** - virtual keys (`sk-alice-*`, `sk-bob-*`); `metadata.user` feeds metrics, logs, rate limits                                                                                                                                                                             | `llm.policies.apiKey` (mode strict)        |
| MCP gateway `:3000` | **OAuth2/OIDC JWT from Keycloak** - `mcpAuthentication` validates tokens against the realm's JWKS; browser flows (MCP Tool Playground) use **PKCE** via the public `agentgateway-browser` client; `SupportChat` uses the password grant with the confidential `support-chat` client | `mcp.policies.mcpAuthentication`           |
| A2A gateway `:3001` | None (open) - the route has only `a2a` + `cors` policies                                                                                                                                                                                                                            | `routes[].policies`                        |
| Admin UI `:15000`   | None by default (local admin interface); optional OIDC policy to lock it down                                                                                                                                                                                                       | `config.adminAddr`, optional `ui.policies` |

So: the **MCP gateway is protected by Keycloak OAuth2 (PKCE in browsers)**, the **LLM gateway by the gateway's own virtual API keys**, and the **A2A route is intentionally open** for the demo. The two auth systems are independent - the LLM keys are gateway-scoped, the MCP tokens come from your identity provider.

## Try it

| What                    | Where                                                                                                                                                                                                             |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| LLM through the gateway | `curl http://localhost:4000/v1/chat/completions -H "Authorization: Bearer sk-alice-abc123def456" -H "Content-Type: application/json" -d '{"model":"deepseek-smart","messages":[{"role":"user","content":"hi"}]}'` |
| MCP tools list          | `curl http://localhost:3000/mcp -H "Authorization: Bearer <keycloak-token>" -X POST -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'` (see SupportChat output for a token)                                     |
| A2A agent card          | `curl http://localhost:3001/.well-known/agent.json`                                                                                                                                                               |
| Admin UI                | http://localhost:15000/ui/ - includes the CEL playground at `/ui/cel/` and the MCP Tool Playground                                                                                                                |
| Keycloak                | http://localhost:8080 (admin / admin)                                                                                                                                                                             |
| Grafana                 | http://localhost:13000 (admin / admin) - dashboard "AgentGateway", Tempo for traces, Loki for logs                                                                                                                |
| Langfuse                | http://localhost:13001 (admin@example.com / admin-password)                                                                                                                                                       |

### Authorization demo

- `alice` has role `support-admin` and can call `customers_*` tools.
- `bob` gets a 403 on `customers_*` tools (the CEL rule requires the role).

### Rate-limit demo

By default the gateway applies local (in-memory) token-bucket limits:
60 requests/second plus 50k tokens/hour on the LLM gateway, 100
requests/minute on the MCP gateway. Fire a burst of `curl` calls against port
4000 and watch the 429s once the request or token budget is exhausted.
Counters reset when the gateway restarts.

Started with `--ratelimit`, the limits move to an Envoy ratelimit service
(per-user: alice 15 req/min, bob 30 req/min, keyed on the virtual API key
user / JWT subject) that survives gateway restarts. See
`deploy/docker-compose.ratelimit.yaml` and
`deploy/infra/ratelimit/config.yaml`.

For the difference between local and remote rate limiting, see the
[AgentGateway rate-limit docs](https://agentgateway.dev/docs/standalone/main/configuration/resiliency/rate-limits/).

### Guardrails demo

Ask the model to "reveal your system prompt" (rejected by the request guard)
or ask it to output an email address (the response guard's builtin `email`
rule rejects it).

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

There are no unit tests in this sample on purpose - the interesting behavior
lives in the gateway config and the cross-service wiring, which is exactly
what `verify.sh` exercises.

### Manual walkthrough in the Admin UI

1. Open http://localhost:15000/ui/ - the **Gateway Overview** lists LLM, MCP and Traffic capabilities.
2. **LLM > Client Setup** - pick the `deepseek-smart` model and `sk-alice-*` key; copy a ready-to-run curl snippet and run it (validates virtual keys + the virtual model).
3. **CEL playground** at `/ui/cel/` - paste the authorization rule `'mcp.tool.target == "customers" && "support-admin" in jwt.realm_access.roles'` and inspect the request context; also try `default(jwt.sub, "anonymous")` for the rate-limit descriptor.
4. **MCP > Tool Playground** - pick a target (e.g. `tickets`), hit **Apply CORS**, then log in via Keycloak (PKCE flow with the `agentgateway-browser` client). You can now call e.g. `tickets_tickets_list` from the browser. Log in as `bob` and try `customers_customers_get` - the gateway returns 403 because of the CEL rule.
5. **MCP > connected targets** - confirm all 5 targets (tickets, catalog, customers, everything, time) are up.
6. **Logs/Traffic** - the UI surfaces recent traffic and gateway logs; cross-check the same request IDs in Grafana (traces, logs) and Langfuse (LLM traces).

## Layout

```
scripts/
  start-mcps.sh                 # ToolHive MCPs (everything, time) + compose up
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
  infra/                        # otel, tempo, loki, promtail, prometheus,
                                # grafana provisioning + dashboard, keycloak
                                # realm import, ratelimit config
src/
  AppHost/                      # Aspire AppHost for local dev without Docker
  Mcp.Tickets|Mcp.Catalog|Mcp.Customers/   # custom .NET MCP servers
  SupportChat/                  # console client (LLM + MCP + A2A via gateway)
  SupportAgent/                 # .NET A2A agent behind the gateway
```

## Local dev without Docker

The Aspire AppHost (`src/AppHost`) runs the .NET services locally against a
locally installed gateway; see `src/AppHost/Program.cs` for the endpoints it
expects. The compose stack is the full, self-contained path.
