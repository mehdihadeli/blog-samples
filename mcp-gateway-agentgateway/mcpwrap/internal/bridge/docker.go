package bridge

// Minimal Docker lifecycle helpers for mcpwrap workloads.
//
// The container is started with `--rm`, so removing the container also kills
// it. All lifecycle calls go through `docker` on PATH (no Docker SDK).

import (
	"fmt"
	"os/exec"
	"strings"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/config"
)

// ContainerArgs builds the `docker run` argv for a workload:
//
//	docker run -i --rm --name mcpwrap-<name> [--network none] [-v ...] [-e ...] <image>
//
// `-i` keeps stdin attached — the stdio transport. No port bindings: the
// container is purely a stdio child; the HTTP surface lives in the proxy.
func ContainerArgs(w *config.Workload) []string {
	args := []string{"run", "-i", "--rm", "--name", w.ContainerName()}
	if w.NoNetwork {
		// The server never opens outbound connections — drop its network
		// entirely instead of leaving a full-bridge container reachable.
		args = append(args, "--network", "none")
	}
	for _, v := range w.Volumes {
		args = append(args, "-v", v)
	}
	for _, e := range w.Env {
		args = append(args, "-e", e)
	}
	args = append(args, w.Image)
	return args
}

// StopContainer force-removes a container by name. Best-effort: a missing
// container is not an error (the --rm flag already cleaned it up).
func StopContainer(name string) error {
	cmd := exec.Command("docker", "rm", "-f", name)
	out, err := cmd.CombinedOutput()
	if err != nil {
		s := strings.TrimSpace(string(out))
		if strings.Contains(s, "No such container") {
			return nil
		}
		return fmt.Errorf("docker rm -f %s: %w (%s)", name, err, s)
	}
	return nil
}
