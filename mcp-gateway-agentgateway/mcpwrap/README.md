# mcpwrap — run stdio MCP servers as Streamable HTTP

> **Scope: teaching / demo only — not for production.** `mcpwrap` exists to
> make the stdio→HTTP bridge visible: it spawns each server container,
> forwards newline-delimited JSON-RPC frames, and mints sessions — exactly
> the machinery that production runtimes like
> [ToolHive](https://docs.stacklok.com/toolhive/) hide behind a single
> `thv run` command. Two hard limits:
>
> - **docker images only** — no `npx://` / `uvx://` protocol schemes, so it
>   cannot host npm-only servers like
>   `@modelcontextprotocol/server-everything` (ToolHive can, via `npx://`)
> - **no production lifecycle** — no auto-restart, no health-based
>   recovery, no egress guardrails (the ToolHive variant adds all three)
>
> For the production story see the sample's
> [README](../README.md#the-three-approaches--which-runtime-should-you-use):
> Approach 2 (ToolHive) is the recommended default; `mcpwrap` is Approach 3.

`mcpwrap` is a small Go CLI that runs stdio-based MCP server containers and
exposes them as **Streamable HTTP** endpoints on `host:port/mcp` — ~300
lines of Go, zero external runtime dependencies (just `docker` and one
statically linked binary).

- `run` one server in the foreground, or `up` a whole fleet from a JSON config
- per-workload network control: `--network none` for servers that never open
  outbound connections (`mcp/memory`, `mcp/sequentialthinking`), default
  Docker networking for servers that do (`mcp/fetch`)
- state persisted in `~/.mcpwrap/state.json`, so `list`/`stop`/`down` work
  from any terminal, even after the process that started the workloads died
- the proxy speaks the MCP Streamable HTTP transport: it mints an
  `Mcp-Session-Id` on `initialize`, matches responses by JSON-RPC `id`, and
  exposes a `/healthz` endpoint per workload

## Why

The companion sample `../mcp-gateway-agentgateway` demonstrates a gateway
(agentgateway) that talks to MCP servers over **Streamable HTTP**. Many MCP
servers only ship as **stdio** binaries — they speak newline-delimited
JSON-RPC on stdin/stdout and expose no HTTP port. `mcpwrap` is a minimal,
readable bridge between the two: it runs each official image as a plain
stdio container, proxies HTTP requests into its stdin, and returns the
matching stdout responses over HTTP.

## Install

Requires Go 1.24+ and a working `docker` CLI (Docker Desktop etc.).

### Option A — `go install` (recommended)

From anywhere, once the module is published:

```sh
go install github.com/mehdihadeli/devops-samples/samples/mcp-gateway-agentgateway/mcpwrap/cmd/mcpwrap@latest
```

Or from a checkout of this repo:

```sh
cd mcpwrap
GOBIN=$HOME/.local/bin go install ./cmd/mcpwrap   # or just: go install ./cmd/mcpwrap
```

The binary lands in `$GOBIN` / `$GOPATH/bin` (`mcpwrap.exe` on Windows).

### Option B — install script

```sh
./install.sh                     # build -> ~/.local/bin/mcpwrap
./install.sh --prefix ~/bin      # build -> ~/bin/mcpwrap
./install.sh --go                # go install ./cmd/mcpwrap (uses $GOBIN)
./install.sh --remote            # go install <module>/cmd/mcpwrap@latest
```

The script prints the exact PATH line to add if the target directory is not
already on your `PATH`.

### Option C — plain build

```sh
cd mcpwrap
go build -o mcpwrap.exe ./cmd/mcpwrap
./mcpwrap.exe --help
```

## Running it as the sample's runtime

The sample `../mcp-gateway-agentgateway` treats `mcpwrap` as its runtime —
the gateway, Keycloak, and observability stack route to `mcpwrap` proxies on
the host:

| file                            | role                                                                 |
| ------------------------------- | -------------------------------------------------------------------- |
| `../mcpwrap.json` (this dir)    | fleet config: memory :19101, fetch :19102, sequentialthinking :19103 |
| `../config.mcpwrap.yaml`        | agentgateway config, targets `host.docker.internal:19101-19103/mcp`  |
| `../docker-compose.mcpwrap.yml` | full stack (gateway mounted on `config.mcpwrap.yaml`)                |
| `../scripts/start-mcpwrap.sh`   | build wrapper → start daemon detached → wait `/healthz` → compose up |
| `../scripts/stop-mcpwrap.sh`    | SIGTERM daemon → `mcpwrap down` → compose down                       |

```bash
./scripts/start-mcpwrap.sh   # from samples/mcp-gateway-agentgateway/
./scripts/stop-mcpwrap.sh
```

The launcher starts `mcpwrap up -f mcpwrap/mcpwrap.json` detached (nohup,
pid in `logs/mcpwrap.pid`, output in `logs/mcpwrap.log`) and waits until all
three proxies answer `/healthz` before bringing the gateway up — the proxies
are host processes, so they die with their terminal and the script restarts
them.

## Install / build

```sh
cd mcpwrap
go build -o mcpwrap.exe ./cmd/mcpwrap   # or: go install ./cmd/mcpwrap
```

Requires Go 1.24+ and a working `docker` CLI (Docker Desktop etc.).

## Commands

```
mcpwrap run <image> [flags]   # one server, foreground
mcpwrap up    [-f mcpwrap.json]  # whole fleet, until Ctrl-C / all exit
mcpwrap list                  # tracked workloads + endpoints
mcpwrap stop <name>           # stop one
mcpwrap down                  # stop everything, clear state
mcpwrap version               # print version
```

### `run`

```sh
mcpwrap run docker.io/mcp/memory --name memory --port 19101 --no-network
mcpwrap run docker.io/mcp/fetch  --name fetch  --port 19102
```

| flag                | meaning                                                                  |
| ------------------- | ------------------------------------------------------------------------ |
| `-p, --port`        | host port for the proxy (**required**)                                   |
| `-n, --name`        | workload/container name (default: derived from image)                    |
| `--host`            | bind address (default `0.0.0.0`, so containers/other hosts can reach it) |
| `--no-network`      | run the container with `--network none`                                  |
| `-v, --volume`      | `docker -v` passthrough, repeatable (`mem-vol:/app/dist`)                |
| `--env`             | `docker -e` passthrough, repeatable (`FOO=bar`)                          |
| `--request-timeout` | per-request upstream timeout (default `60s`)                             |

`run` blocks until the container exits or the proxy fails; the workload is
removed from state on exit.

### `up` with a config file

```jsonc
// mcpwrap.json
{
  "workloads": {
    "memory": {
      "image": "docker.io/mcp/memory",
      "port": 19101,
      "noNetwork": true,
    },
    "fetch": { "image": "docker.io/mcp/fetch", "port": 19102 },
    "sequentialthinking": {
      "image": "docker.io/mcp/sequentialthinking",
      "port": 19103,
      "noNetwork": true,
    },
  },
}
```

```sh
mcpwrap up -f mcpwrap.json
```

Starts every workload in its own goroutine (a crash in one does not stop the
others), prints the endpoint table, and tears everything down on Ctrl-C — or
when all workloads have exited (e.g. `mcpwrap down` from another terminal).

## The bridge, in one paragraph

MCP Streamable HTTP maps **one JSON-RPC message to one HTTP request**; MCP
stdio maps **one JSON-RPC message to one newline-delimited line**. `mcpwrap`
therefore:

1. receives `POST /mcp` with a JSON-RPC body,
2. trims any trailing CR/LF and forwards the line (`+ "\n"`) into the
   container's stdin (`docker run -i --rm ...`),
3. waits for the matching response line on stdout, matched by JSON-RPC `id`,
4. returns it as the HTTP response with an `Mcp-Session-Id` header.

Sessions: the first request (`initialize`) without an `mcp-session-id` header
gets a freshly generated id, echoed back in the response header; the client
reuses it on subsequent requests. Requests are serialized on the stdin pipe
(stdio servers process one message at a time); the `requests` map is guarded
by a mutex in short critical sections only — holding it across the wait would
deadlock, because the stdout reader needs the same lock to deliver responses.

```
client ──HTTP──▶ POST /mcp {jsonrpc...id:1}
                    │  mcpwrap proxy (per workload)
                    ▼
            docker run -i --rm --name mcpwrap-<name> <image>
                    │  stdin:  {...id:1}\n
                    │  stdout: {...id:1}\n
                    ▼
client ◀──HTTP── 200 {"jsonrpc":...,"id":1,...} + Mcp-Session-Id
```

## Layout

```
mcpwrap/
├── cmd/mcpwrap/main.go        # thin entry → cli.Execute()
├── internal/
│   ├── config/config.go       # Workload + Config, Load(), helpers
│   ├── state/state.go         # ~/.mcpwrap/state.json persistence
│   ├── bridge/docker.go       # docker run arg building, stop
│   ├── bridge/proxy.go        # the stdio ↔ HTTP bridge
│   └── cli/                   # cobra commands (run/up/list/stop/down/version)
├── install.sh                 # build + install helper (see Install)
├── mcpwrap.json               # sample fleet config
└── go.mod / go.sum
```

## Network model

`mcpwrap` keeps it simple: each workload is one container with the stock
image, run with `-i --rm`. Two network modes:

- `--no-network` — `docker run --network none`: the container has no
  network interface at all. Perfect for servers that only answer on stdio
  and never open outbound connections (`mcp/memory`, `mcp/sequentialthinking`).
- default — the container uses Docker's default bridge, so it can reach the
  internet. Needed for servers whose tools fetch remote content
  (`mcp/fetch`).

No sidecars, no extra proxies, no per-workload permission profiles — the
host firewall is the boundary. For servers that need outbound access, the
default bridge is the whole story.
