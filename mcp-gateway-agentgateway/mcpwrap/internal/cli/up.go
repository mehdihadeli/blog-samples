package cli

import (
	"fmt"
	"os"
	"os/signal"
	"sync"
	"syscall"

	"github.com/spf13/cobra"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/bridge"
	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/config"
	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/state"
)

// newUpCmd: mcpwrap up -f mcpwrap.json — all workloads, until Ctrl-C.
func newUpCmd() *cobra.Command {
	var configPath string

	cmd := &cobra.Command{
		Use:   "up",
		Short: "Start all workloads from a config file",
		Long: `Start every workload defined in the config (default mcpwrap.json)
and keep them running until Ctrl-C, then tear everything down. Each workload
runs in its own goroutine: a crash in one does not stop the others.`,
		Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			cfg, err := config.Load(configPath)
			if err != nil {
				return err
			}
			names := make([]string, 0, len(cfg.Workloads))
			for n := range cfg.Workloads {
				names = append(names, n)
			}
			if len(names) == 0 {
				return fmt.Errorf("no workloads defined in %s", configPath)
			}

			// Port conflicts are the only realistic collision — check up front.
			seen := map[int]string{}
			for _, n := range names {
				w := cfg.Workloads[n]
				if prev, dup := seen[w.Port]; dup {
					return fmt.Errorf("duplicate port %d: %s and %s", w.Port, prev, n)
				}
				seen[w.Port] = n
			}

			// Save state, then start every workload in its own goroutine.
			s := state.Load()
			for _, n := range names {
				w := cfg.Workloads[n]
				w.Name = n // config keys are the workload names
				s.Add(&w)
			}
			if err := s.Save(); err != nil {
				return err
			}

			stopOnSignal := make(chan os.Signal, 1)
			signal.Notify(stopOnSignal, os.Interrupt, syscall.SIGTERM)

			var wg sync.WaitGroup
			allDone := make(chan struct{})
			for _, n := range names {
				w := cfg.Workloads[n]
				w.Name = n
				wg.Add(1)
				go func() {
					defer wg.Done()
					fmt.Printf("==> starting %s (%s) on %s:%d/mcp  [network: %s]\n",
						w.Name, w.Image, w.BindAddr(), w.Port, w.NetworkLabel())
					if err := bridge.Run(&w); err != nil {
						fmt.Fprintf(os.Stderr, "workload %s exited: %v\n", w.Name, err)
					}
				}()
			}
			go func() {
				wg.Wait()
				close(allDone)
			}()

			fmt.Println()
			fmt.Println("mcpwrap workloads (Ctrl-C to stop all):")
			fmt.Println("  name                endpoint                              network")
			for _, n := range names {
				w := cfg.Workloads[n]
				fmt.Printf("  %-18s http://%s:%d/mcp                %s\n", w.Name, w.BindAddr(), w.Port, w.NetworkLabel())
			}

			select {
			case <-stopOnSignal:
				fmt.Println("\n==> stopping all workloads")
			case <-allDone:
				// Every workload exited on its own (crash, docker rm, down
				// from another terminal) — tear down whatever is left.
				fmt.Println("\n==> all workloads exited")
			}
			for _, n := range names {
				_ = bridge.StopContainer("mcpwrap-" + n)
			}
			s = state.Load()
			s.Clear()
			_ = s.Save()
			wg.Wait()
			return nil
		},
	}

	cmd.Flags().StringVarP(&configPath, "config", "f", "mcpwrap.json", "JSON config file with the workloads to start")
	return cmd
}
