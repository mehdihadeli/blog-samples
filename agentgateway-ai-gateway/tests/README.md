# AgentGateway Sample Tests

These are xUnit v3 integration tests. They call the running AgentGateway,
Keycloak, and ToolHive MCP proxies; they are not isolated unit tests.

## Prerequisites

From `samples/agentgateway-ai-gateway`:

1. Put values in `deployments/.env`:

   ```dotenv
   DEEPSEEK_API_KEY=your-deepseek-api-key
   DEEPSEEK_ENDPOINT=api.deepseek.com:443
   ```

2. Start the external MCP proxies before running tests:

   ```bash
   ./scripts/start-mcps.sh
   ```

   On Windows, run this Bash script from Git Bash or WSL. It starts the
   registry-backed `mcp-everything` on port `19101` and
   `mcp-sequentialthinking` on port `19103`.

3. Start the Compose stack:

   ```bash
   docker compose -f deployments/docker-compose.yaml up -d --build
   ```

   Compose publishes only YARP on `5000`. MCP, LLM, A2A, Admin, metrics, and
   Keycloak are reachable through path-based routes on that port.

## Run Tests

Run from the sample root:

```bash
dotnet test tests/AgentGateway.Samples.Tests/AgentGateway.Samples.Tests.csproj
```

PowerShell does not automatically load `.env` files. Set provider variables
in the current PowerShell session if provider-backed tests should run:

```powershell
$env:DEEPSEEK_API_KEY = (Get-Content deployments/.env | Where-Object { $_ -match '^DEEPSEEK_API_KEY=' }) -replace '^DEEPSEEK_API_KEY=', ''
$env:DEEPSEEK_ENDPOINT = (Get-Content deployments/.env | Where-Object { $_ -match '^DEEPSEEK_ENDPOINT=' }) -replace '^DEEPSEEK_ENDPOINT=', ''
```

Tests that require the external provider are skipped when
`DEEPSEEK_API_KEY` is absent. Tests requiring unavailable services fail rather
than silently passing, so start ToolHive and Compose first.

## Cleanup

Stop the Compose stack and external MCP proxies after testing:

```bash
docker compose -f deployments/docker-compose.yaml down
./scripts/stop-mcps.sh
```
