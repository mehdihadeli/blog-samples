// Command mcpwrap runs stdio-based MCP server containers and exposes them
// as Streamable HTTP endpoints on host:port/mcp — a small, dependency-light
// Go CLI with zero external runtime dependencies (just docker).
package main

import (
	"os"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/cli"
)

func main() {
	if err := cli.Execute(); err != nil {
		os.Exit(1)
	}
}
