package cli

import (
	"fmt"

	"github.com/spf13/cobra"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/bridge"
	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/config"
	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/state"
)

// newRunCmd: mcpwrap run <image> [flags] — one server, foreground.
func newRunCmd() *cobra.Command {
	var (
		name       string
		port       int
		host       string
		noNetwork  bool
		volumes    []string
		envs       []string
		reqTimeout string
	)

	cmd := &cobra.Command{
		Use:   "run <image>",
		Short: "Start one MCP server (foreground)",
		Long: `Start one MCP server container and expose it as Streamable HTTP on
host:port/mcp. Blocks until the container exits or the proxy fails.

Network model:
  default      container keeps normal Docker networking -- for servers that
               need outbound access (mcp/fetch)
  --no-network docker run --network none -- for servers that never open
               connections (mcp/memory, mcp/sequentialthinking)`,
		Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			w := &config.Workload{
				Name:      name,
				Image:     args[0],
				Port:      port,
				Host:      host,
				NoNetwork: noNetwork,
				Volumes:   volumes,
				Env:       envs,
				Timeout:   reqTimeout,
			}
			if w.Name == "" {
				w.Name = config.NameFromImage(w.Image)
			}

			s := state.Load()
			s.Add(w)
			if err := s.Save(); err != nil {
				return err
			}
			fmt.Printf("==> starting %s (%s) on %s:%d/mcp  [network: %s]\n",
				w.Name, w.Image, w.BindAddr(), w.Port, w.NetworkLabel())
			defer func() {
				_ = bridge.StopContainer(w.ContainerName())
				s := state.Load()
				s.Remove(w.Name)
				_ = s.Save()
			}()
			return bridge.Run(w)
		},
	}

	cmd.Flags().StringVarP(&name, "name", "n", "", "workload/container name (default: derived from image)")
	cmd.Flags().IntVarP(&port, "port", "p", 0, "host port for the Streamable HTTP proxy")
	cmd.Flags().StringVar(&host, "host", "0.0.0.0", "bind address for the proxy")
	cmd.Flags().BoolVar(&noNetwork, "no-network", false, "run the container with --network none (no outbound)")
	cmd.Flags().StringSliceVar(&volumes, "volume", nil, "docker -v passthrough, repeatable (e.g. mem-vol:/app/dist)")
	cmd.Flags().StringSliceVar(&envs, "env", nil, "docker -e passthrough, repeatable (e.g. FOO=bar)")
	cmd.Flags().StringVar(&reqTimeout, "request-timeout", "60s", "per-request upstream timeout")
	_ = cmd.MarkFlagRequired("port")
	return cmd
}
