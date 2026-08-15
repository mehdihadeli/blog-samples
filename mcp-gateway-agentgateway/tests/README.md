# MCP Gateway Tests (xUnit v3 + Shouldly)

Automated verification for the agentgateway MCP gateway sample — the .NET
replacement for the old `scripts/verify.mjs`. Speaks the MCP Streamable HTTP
protocol (JSON-RPC 2.0) directly against the running gateway.

## Prerequisites

- .NET SDK 9.0+
- The gateway stack is running (`scripts/start-toolhive.sh` in the sample root)

## Run

```powershell
cd tests/McpGateway.Tests
dotnet test
```

If the gateway is not reachable, tests **skip** (xUnit v3 dynamic skip)
instead of failing with connection errors.

## Configuration (environment variables)

| Variable      | Default                   | Meaning          |
| ------------- | ------------------------- | ---------------- |
| `GATEWAY_URL` | `http://localhost:18080`  | Gateway base URL |
| `MCP_API_KEY` | `sk-mcp-gateway-demo-key` | Gateway API key  |

## What is covered (16 assertions)

| #   | Route       | Check                                                                                   |
| --- | ----------- | --------------------------------------------------------------------------------------- |
| 1   | —           | Request without API key is rejected (401/403)                                           |
| 2   | `/memory`   | `tools/list` exposes the 6 memory tools (`create_entities`, ...)                        |
| 3   | `/fetch`    | `tools/list` exposes `fetch`                                                            |
| 4   | `/thinking` | `tools/list` exposes `sequentialthinking`                                               |
| 5   | `/mcp`      | Federated tools are prefixed (`memory_*`, `fetch_fetch`, `thinking_sequentialthinking`) |
| 6   | `/memory`   | E2E `create_entities` (Alice + Bob) -> `read_graph`                                     |
| 7   | `/mcp`      | E2E `memory_create_entities` (Carol) -> `memory_read_graph`                             |
| 8   | `/mcp`      | E2E `fetch_fetch` https://example.com returns "Example Domain"                          |
| 9   | `/thinking` | E2E `sequentialthinking` returns `thoughtNumber`                                        |
| 10  | `/mcp`      | E2E `thinking_sequentialthinking` returns `thoughtHistoryLength`                        |
| 11  | `:8082`     | Keycloak JWT (`Authorization: Bearer`) authenticates on the SSO port                    |
