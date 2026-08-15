package cli

import (
	"fmt"

	"github.com/spf13/cobra"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/bridge"
	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/state"
)

// newListCmd: mcpwrap list — show tracked workloads + endpoints.
func newListCmd() *cobra.Command {
	return &cobra.Command{
		Use:   "list",
		Short: "Show tracked workloads and their endpoints",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			s := state.Load()
			names := s.Names()
			if len(names) == 0 {
				fmt.Println("no tracked workloads (mcpwrap up / run)")
				return nil
			}
			fmt.Println("tracked workloads:")
			fmt.Println("  name                endpoint                              network        container")
			for _, n := range names {
				w, _ := s.Get(n)
				fmt.Printf("  %-18s http://%s:%d/mcp                %-14s %s\n",
					n, w.BindAddr(), w.Port, w.NetworkLabel(), w.ContainerName())
			}
			return nil
		},
	}
}

// newStopCmd: mcpwrap stop <name> — stop one workload.
func newStopCmd() *cobra.Command {
	return &cobra.Command{
		Use:   "stop <name>",
		Short: "Stop one workload",
		Args:  cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			name := args[0]
			if err := bridge.StopContainer("mcpwrap-" + name); err != nil {
				return err
			}
			s := state.Load()
			s.Remove(name)
			return s.Save()
		},
	}
}

// newDownCmd: mcpwrap down — stop everything.
func newDownCmd() *cobra.Command {
	return &cobra.Command{
		Use:   "down",
		Short: "Stop everything started via this tool",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			s := state.Load()
			names := s.Names()
			if len(names) == 0 {
				fmt.Println("nothing to stop (no tracked workloads)")
				return nil
			}
			for _, n := range names {
				if err := bridge.StopContainer("mcpwrap-" + n); err != nil {
					return err
				}
			}
			s.Clear()
			return s.Save()
		},
	}
}
