// Package state persists the set of workloads started via mcpwrap
// (~/.mcpwrap/state.json) so `list`, `stop` and `down` work across
// invocations without the proxy processes running.
package state

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sort"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/config"
)

const fileName = "state.json"

// File is the on-disk state: workload name -> workload definition.
type File struct {
	Workloads map[string]*config.Workload `json:"workloads"`
	path      string
}

// Load reads the state file (missing file = empty state).
func Load() *File {
	f := &File{Workloads: map[string]*config.Workload{}}
	f.path = defaultPath()
	b, err := os.ReadFile(f.path)
	if err != nil {
		return f
	}
	if err := json.Unmarshal(b, f); err != nil {
		return f
	}
	if f.Workloads == nil {
		f.Workloads = map[string]*config.Workload{}
	}
	return f
}

func defaultPath() string {
	home, err := os.UserHomeDir()
	if err != nil {
		return fileName // fall back to cwd
	}
	return filepath.Join(home, ".mcpwrap", fileName)
}

// Path returns where the state file lives.
func (f *File) Path() string { return f.path }

// Save writes the state file, creating its directory if needed.
func (f *File) Save() error {
	if err := os.MkdirAll(filepath.Dir(f.path), 0o755); err != nil {
		return err
	}
	b, err := json.MarshalIndent(f, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(f.path, b, 0o644)
}

// Add upserts a workload (keyed by its Name).
func (f *File) Add(w *config.Workload) {
	f.Workloads[w.Name] = w
}

// Remove deletes a workload by name.
func (f *File) Remove(name string) {
	delete(f.Workloads, name)
}

// Clear empties the workload set.
func (f *File) Clear() {
	f.Workloads = map[string]*config.Workload{}
}

// Names returns the workload names in sorted order.
func (f *File) Names() []string {
	names := make([]string, 0, len(f.Workloads))
	for n := range f.Workloads {
		names = append(names, n)
	}
	sort.Strings(names)
	return names
}

// Get returns a workload by name.
func (f *File) Get(name string) (*config.Workload, bool) {
	w, ok := f.Workloads[name]
	return w, ok
}
