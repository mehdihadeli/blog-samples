package cli

import (
	"fmt"

	"github.com/spf13/cobra"
)

// newVersionCmd: mcpwrap version (cobra also adds --version on the root).
func newVersionCmd() *cobra.Command {
	return &cobra.Command{
		Use:   "version",
		Short: "Print the version",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			fmt.Println("mcpwrap " + Version)
			return nil
		},
	}
}
