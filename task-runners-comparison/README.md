# Task Runners Comparison

This sample is the runnable companion for the blog post comparing Make, Just, Mise, and go-task for a .NET Aspire app in 2026.

It uses an Aspire starter application generated with the Aspire CLI and keeps four equivalent task definitions at the repository root:

- `mise.toml`
- `Justfile`
- `Taskfile.yml`
- `Makefile`

The sample intentionally keeps the lifecycle small and boring:

- restore
- build
- test
- format
- clean
- hook
- prepare
- run

Each runner restores the solution explicitly with `dotnet restore TaskRunnersComparison.slnx` because the repository root contains multiple projects plus both `.sln` and `.slnx` files.

## Prerequisites

- [mise](https://mise.jdx.dev/) for tool version management
- [Aspire CLI](https://aspire.dev/get-started/install-cli/) for local app orchestration

Optional, if you want to try the other task runners directly instead of using `mise`:

- [just](https://just.systems/)
- [Task](https://taskfile.dev/)
- [GNU Make](https://www.gnu.org/software/make/)

## Run locally

```bash
# 1. Clone the repository
git clone git@github.com:mehdihadeli/blog-samples.git

# 2. Navigate to the sample directory
cd blog-samples/task-runners-comparison

# 3. Install tools (.NET SDK and Node.js from mise.toml)
mise install

# 4. First-time setup
mise run prepare

# 5. Start the application
mise run run
```

If you want to use the other runners after installing the tools, these are the equivalent startup commands:

```bash
just prepare && just run
task prepare && task run
make prepare && make run
```

## Why the formatter command uses `dotnet csharpier`

The local tool manifest installs CSharpier and this environment does not have `dnx` available, so the sample uses:

```bash
dotnet csharpier format .
```

If your machine has `dnx` and you prefer it, you can swap the formatter command in each task file.

## Quick start

### Mise

```bash
mise install
mise run prepare
mise run build
mise run run
```

### Just

```bash
just prepare
just build
just run
```

### go-task

```bash
task prepare
task build
task run
```

### Make

```bash
make prepare
make build
make run
```

## Notes

- `TaskRunnersComparison.slnx` is the solution file used by the task definitions.
- `git config core.hooksPath .husky` is included as a realistic setup step even though this sample does not ship Husky hooks.
- `aspire start` starts the AppHost in the background for local development.
