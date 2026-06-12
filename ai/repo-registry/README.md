# Repo Registry MCP Server Sample

This sample is a dedicated `RepoRegistry.McpServer` project with a normal C# service layer behind it. The actual server path uses the official `ModelContextProtocol` stdio host so the repo-registry tools are exposed as real MCP tools, while the extra CLI tool-invocation mode stays available as a smoke-test helper.

## Commands

```bash
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json describe
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json serve-stdio
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json --tool list_repos
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json --tool get_workspace_status
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json --tool get_repo_info Shared.Contracts
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json --tool find_dependents Shared.Contracts
dotnet run --project ./src/RepoRegistry.McpServer -- --registry ./sample-data/repos.json --tool search_across_repos CustomerSummaryResponse "*.cs"
```

Use `serve-stdio` when you want the real MCP server process. The `describe` and `--tool ...` commands are convenience paths for local inspection and shell-based tests in this sample repo.

## Run the sample test

```bash
bash ./tests/test-repo-registry.sh
```
