.DEFAULT_GOAL := help

.PHONY: help restore build test format-cs format clean hook prepare run

help:
	@echo "Available targets:"
	@echo "  make restore      Restore NuGet packages and tools"
	@echo "  make build        Build the solution"
	@echo "  make test         Run tests"
	@echo "  make format-cs    Format C# code"
	@echo "  make format       Format all code"
	@echo "  make clean        Clean build artifacts"
	@echo "  make hook         Setup pre-commit hooks"
	@echo "  make prepare      First-time setup after cloning"
	@echo "  make run          Run the application with Aspire"

restore:
	dotnet restore TaskRunnersComparison.slnx
	dotnet tool restore

build: restore
	dotnet build TaskRunnersComparison.slnx

test: build
	dotnet test TaskRunnersComparison.slnx

format-cs:
	dotnet csharpier format .

format: format-cs

clean:
	dotnet clean TaskRunnersComparison.slnx

hook:
	git config core.hooksPath .husky

prepare: restore hook

run:
	aspire start