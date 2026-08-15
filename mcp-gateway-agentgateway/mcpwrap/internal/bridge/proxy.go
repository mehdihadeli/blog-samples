package bridge

// The stdio -> Streamable HTTP bridge.
//
// MCP Streamable HTTP (https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
// maps one JSON-RPC message to one HTTP request. The MCP stdio transport
// maps one JSON-RPC message to one newline-delimited line on stdout. This
// proxy therefore:
//
//  1. receives POST /mcp with a JSON-RPC body,
//  2. forwards the raw message (+ "\n") into the container's stdin,
//  3. waits for the matching JSON-RPC response line on stdout,
//  4. returns it as the HTTP response with an Mcp-Session-Id header.
//
// Sessions: the first request (initialize) without an mcp-session-id header
// gets a freshly generated id, echoed back in the Mcp-Session-Id response
// header; the client reuses it on subsequent requests (the gateway does).
//
// Concurrency: HTTP requests for one workload are serialized on the stdin
// pipe (stdinMu) — MCP stdio servers process one message at a time. Each
// request is matched to its response by JSON-RPC id, so stray server
// notifications are dropped and responses find their waiting request even if
// the server answered out of order. The requests map is guarded by reqMu in
// short critical sections only (register / unregister / lookup).

import (
	"bufio"
	"bytes"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"strings"
	"sync"
	"time"

	"github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/internal/config"
)

// Run starts the container + HTTP server for one workload and blocks until
// either the container exits or the HTTP server fails. Returns after
// cleanup.
func Run(w *config.Workload) error {
	p, err := start(w)
	if err != nil {
		return err
	}

	addr := fmt.Sprintf("%s:%d", w.BindAddr(), w.Port)
	mux := http.NewServeMux()
	mux.HandleFunc("/mcp", p.handleMCP)
	mux.HandleFunc("/healthz", func(rw http.ResponseWriter, _ *http.Request) {
		rw.WriteHeader(http.StatusOK)
		fmt.Fprintln(rw, "ok")
	})
	srv := &http.Server{Addr: addr, Handler: mux}

	containerDone := make(chan error, 1)
	httpDone := make(chan error, 1)
	go func() { containerDone <- p.cmd.Wait() }()
	go func() { httpDone <- srv.ListenAndServe() }()

	select {
	case err := <-containerDone:
		// Container exited (crash, or someone ran docker rm -f) — take the
		// HTTP server down with it.
		_ = srv.Close()
		if err != nil {
			return fmt.Errorf("container %s exited: %w", w.ContainerName(), err)
		}
		return fmt.Errorf("container %s exited", w.ContainerName())
	case err := <-httpDone:
		// HTTP server failed (e.g. port already bound) — kill the container.
		_ = p.cmd.Process.Kill()
		return err
	}
}

// proxy owns the container process, its stdin pipe, and the HTTP server.
type proxy struct {
	w        *config.Workload
	cmd      *exec.Cmd
	stdin    io.WriteCloser
	stdout   io.ReadCloser
	requests map[string]chan []byte // jsonrpc id -> response line

	// stdinMu serializes writes into the container's stdin; reqMu guards the
	// requests map (short critical sections only).
	stdinMu sync.Mutex
	reqMu   sync.Mutex
}

// start spawns `docker run -i --rm ... <image>` with stdin/stdout pipes.
func start(w *config.Workload) (*proxy, error) {
	args := ContainerArgs(w)
	logf("[%s] docker %s", w.Name, strings.Join(args, " "))
	cmd := exec.Command("docker", args...)
	cmd.Stderr = newPrefixedWriter("[%s] ", w.Name)

	stdin, err := cmd.StdinPipe()
	if err != nil {
		return nil, fmt.Errorf("stdin pipe: %w", err)
	}
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return nil, fmt.Errorf("stdout pipe: %w", err)
	}
	if err := cmd.Start(); err != nil {
		return nil, fmt.Errorf("docker run: %w", err)
	}

	p := &proxy{
		w:        w,
		cmd:      cmd,
		stdin:    stdin,
		stdout:   stdout,
		requests: map[string]chan []byte{},
	}
	go p.readLoop()
	return p, nil
}

// readLoop consumes the container's stdout line by line and hands every
// JSON-RPC response (a line with an "id") to the waiting HTTP request.
func (p *proxy) readLoop() {
	sc := bufio.NewScanner(p.stdout)
	sc.Buffer(make([]byte, 1024*1024), 64*1024*1024)
	for sc.Scan() {
		line := append([]byte(nil), sc.Bytes()...)
		logf("[%s] stdout %d bytes: %.160s", p.w.Name, len(line), line)
		var probe struct {
			ID json.RawMessage `json:"id"`
		}
		if err := json.Unmarshal(line, &probe); err != nil || len(probe.ID) == 0 {
			// Notification or malformed line — nothing is waiting for it.
			continue
		}
		id := string(probe.ID)
		p.reqMu.Lock()
		ch := p.requests[id]
		delete(p.requests, id)
		p.reqMu.Unlock()
		if ch != nil {
			select {
			case ch <- line:
			default: // waiter already timed out
			}
		}
	}
}

// handleMCP implements the MCP Streamable HTTP endpoint.
func (p *proxy) handleMCP(rw http.ResponseWriter, r *http.Request) {
	if r.Method == http.MethodDelete {
		rw.WriteHeader(http.StatusNoContent)
		return
	}
	if r.Method != http.MethodPost {
		http.Error(rw, "method not allowed (POST expected)", http.StatusMethodNotAllowed)
		return
	}

	body, err := io.ReadAll(r.Body)
	if err != nil {
		http.Error(rw, "read body: "+err.Error(), http.StatusBadRequest)
		return
	}
	var msg struct {
		JSONRPC string          `json:"jsonrpc"`
		ID      json.RawMessage `json:"id"`
		Method  string          `json:"method"`
	}
	if err := json.Unmarshal(body, &msg); err != nil {
		http.Error(rw, "invalid JSON-RPC: "+err.Error(), http.StatusBadRequest)
		return
	}
	sess := r.Header.Get("mcp-session-id")
	if sess == "" {
		sess = newSessionID()
	}

	// JSON-RPC notification (e.g. notifications/initialized) — no "id", no
	// response expected. Forward it upstream and ACK with 202 Accepted per
	// the Streamable HTTP spec; the readLoop drops any stray stdout lines
	// that don't carry an id, so nothing needs registering here.
	if len(msg.ID) == 0 {
		logf("[%s] notify %s %d bytes method=%s sess=%s", p.w.Name, r.Method, len(body), msg.Method, sess)
		line := append(bytes.TrimRight(body, "\r\n"), '\n')
		p.stdinMu.Lock()
		_, err = p.stdin.Write(line)
		p.stdinMu.Unlock()
		if err != nil {
			http.Error(rw, "upstream stdin: "+err.Error(), http.StatusBadGateway)
			return
		}
		rw.Header().Set("Mcp-Session-Id", sess)
		rw.WriteHeader(http.StatusAccepted)
		return
	}
	logf("[%s] req %s %d bytes id=%s sess=%s", p.w.Name, r.Method, len(body), string(msg.ID), sess)

	// Register this request under its JSON-RPC id, then drop the lock: the
	// readLoop needs reqMu to deliver the response, so holding it across the
	// wait below would deadlock.
	p.reqMu.Lock()
	id := string(msg.ID)
	ch := make(chan []byte, 1)
	p.requests[id] = ch
	p.reqMu.Unlock()

	// Serialize into a single line for the stdio transport: strip any
	// trailing CR/LF the HTTP body may carry (curl --data-binary "@file"
	// keeps the file's trailing newline) and append exactly one "\n".
	line := append(bytes.TrimRight(body, "\r\n"), '\n')

	p.stdinMu.Lock()
	_, err = p.stdin.Write(line)
	p.stdinMu.Unlock()
	if err != nil {
		p.reqMu.Lock()
		delete(p.requests, id)
		p.reqMu.Unlock()
		http.Error(rw, "upstream stdin: "+err.Error(), http.StatusBadGateway)
		return
	}

	timeout := p.w.TimeoutDuration()
	select {
	case resp := <-ch:
		rw.Header().Set("Content-Type", "application/json")
		rw.Header().Set("Mcp-Session-Id", sess)
		rw.WriteHeader(http.StatusOK)
		_, _ = rw.Write(resp)
	case <-time.After(timeout):
		p.reqMu.Lock()
		delete(p.requests, id)
		p.reqMu.Unlock()
		http.Error(rw, "upstream stdio timeout after "+timeout.String(), http.StatusGatewayTimeout)
	}
}

// --- small helpers ---------------------------------------------------------

func newSessionID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}

func logf(format string, a ...any) {
	fmt.Fprintf(os.Stderr, format+"\n", a...)
}

// prefixedWriter prefixes every line written to it (used for the container's
// stderr so logs from concurrent workloads stay distinguishable).
type prefixedWriter struct {
	prefix string
	mu     sync.Mutex
}

func newPrefixedWriter(format string, a ...any) *prefixedWriter {
	return &prefixedWriter{prefix: fmt.Sprintf(format, a...)}
}

func (w *prefixedWriter) Write(p []byte) (int, error) {
	w.mu.Lock()
	defer w.mu.Unlock()
	for _, line := range strings.Split(strings.TrimRight(string(p), "\n"), "\n") {
		if strings.TrimSpace(line) == "" {
			continue
		}
		fmt.Fprintln(os.Stderr, w.prefix+line)
	}
	return len(p), nil
}
