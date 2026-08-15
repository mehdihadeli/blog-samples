// Package config defines the mcpwrap workload schema (mcpwrap.json) and the
// derived names/addresses used at runtime.
package config

import (
	"encoding/json"
	"fmt"
	"os"
	"strings"
	"time"
)

// Workload describes one MCP server: the container image to run, the host
// port its Streamable HTTP proxy binds, and how much network the container
// gets.
type Workload struct {
	// Name is set at runtime (config key or --name), never read from JSON.
	Name      string   `json:"name,omitempty"`
	Image     string   `json:"image"`
	Port      int      `json:"port"`
	Host      string   `json:"host,omitempty"`
	NoNetwork bool     `json:"noNetwork,omitempty"`
	Volumes   []string `json:"volumes,omitempty"`
	Env       []string `json:"env,omitempty"`
	Timeout   string   `json:"timeout,omitempty"`
}

// Config is the root of mcpwrap.json.
type Config struct {
	Workloads map[string]Workload `json:"workloads"`
}

// Load reads and validates a config file.
func Load(path string) (*Config, error) {
	b, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}
	var cfg Config
	if err := json.Unmarshal(b, &cfg); err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}
	if cfg.Workloads == nil {
		return nil, fmt.Errorf("%s: missing \"workloads\" object", path)
	}
	for name, w := range cfg.Workloads {
		if w.Image == "" {
			return nil, fmt.Errorf("%s: workload %q has no image", path, name)
		}
		if w.Port == 0 {
			return nil, fmt.Errorf("%s: workload %q has no port", path, name)
		}
	}
	return &cfg, nil
}

// BindAddr returns the proxy bind address (default 0.0.0.0 so containers
// and other host processes can reach the proxy).
func (w *Workload) BindAddr() string {
	if w.Host == "" {
		return "0.0.0.0"
	}
	return w.Host
}

// ContainerName is the docker --name for this workload.
func (w *Workload) ContainerName() string {
	return "mcpwrap-" + w.Name
}

// NetworkLabel describes the container network mode.
func (w *Workload) NetworkLabel() string {
	if w.NoNetwork {
		return "none"
	}
	return "default"
}

// TimeoutDuration parses the per-request upstream timeout (default 60s).
func (w *Workload) TimeoutDuration() time.Duration {
	if w.Timeout == "" {
		return 60 * time.Second
	}
	d, err := time.ParseDuration(w.Timeout)
	if err != nil {
		return 60 * time.Second
	}
	return d
}

// NameFromImage turns "docker.io/mcp/memory" into "memory".
func NameFromImage(image string) string {
	trimmed := strings.TrimPrefix(image, "docker.io/")
	parts := strings.Split(trimmed, "/")
	last := parts[len(parts)-1]
	last = strings.Split(last, ":")[0] // drop tag
	last = strings.ReplaceAll(last, ".", "-")
	return last
}
