// Package cli wires the mcpwrap commands together with spf13/cobra.
package cli

import (
	"github.com/spf13/cobra"
)

// Version is the build version (overridable at link time via
// -ldflags "-X github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/cli.Version=...").
var Version = "0.1.0"

// Execute runs the root command.
func Execute() error {
	return NewRootCmd().Execute()
}

// NewRootCmd builds the mcpwrap root command with all subcommands.
func NewRootCmd() *cobra.Command {
	root := &cobra.Command{
		Use:     "mcpwrap",
		Short:   "Bridge stdio MCP containers to Streamable HTTP",
		Long: `mcpwrap runs stdio-based MCP server containers and exposes them as
Streamable HTTP endpoints on host:port/mcp -- a small Go CLI with zero
external runtime dependencies (just docker and one binary).

  run    one server (foreground)
  up     all servers from mcpwrap.json
  list   show tracked workloads
  stop   stop one workload
  down   stop everything`,
		SilenceUsage:  true,
		SilenceErrors: true,
		Version:       Version,
	}

	root.AddCommand(
		newRunCmd(),
		newUpCmd(),
		newListCmd(),
		newStopCmd(),
		newDownCmd(),
		newVersionCmd(),
	)
	return root
}
