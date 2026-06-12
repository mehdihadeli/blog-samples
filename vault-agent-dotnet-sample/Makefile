# Makefile for updating .NET packages
# Requires: dotnet-outdated tool (install with: dotnet tool install --global dotnet-outdated)

.PHONY: help install-tool check-outdated update-dry-run update update-interactive update-major clean restore build test all

# Default target
help:
	@echo "Available targets:"
	@echo "  make install-tool       - Install dotnet-outdated global tool"
	@echo "  make check-outdated     - Check for outdated packages without updating"
	@echo "  make update-dry-run     - Preview what would be updated (dry run)"
	@echo "  make update             - Update all packages non-interactively"
	@echo "  make update-interactive - Update packages interactively"
	@echo "  make update-major       - Include major version updates (use with caution)"
	@echo "  make clean             - Clean solution"
	@echo "  make restore           - Restore packages"
	@echo "  make build             - Build solution"
	@echo "  make test              - Run tests"
	@echo "  make all               - Full update cycle: clean, restore, update, build, test"

# Install dotnet-outdated tool
install-tool:
	@echo "Installing dotnet-outdated tool..."
	dotnet tool install --global dotnet-outdated
	@echo "Tool installed successfully. You may need to restart your terminal."

# Check for outdated packages (read-only)
check-outdated:
	@echo "Checking for outdated packages..."
	dotnet outdated

# Dry run - preview updates without actually updating
update-dry-run:
	@echo "Previewing package updates (dry run)..."
	dotnet outdated -u --dry-run

# Update all packages automatically (non-interactive)
update:
	@echo "Updating all packages to latest stable versions..."
	dotnet outdated -u --include-auto

# Update packages interactively (asks for confirmation per package)
update-interactive:
	@echo "Updating packages interactively..."
	dotnet outdated -u

# Include major version updates (potentially breaking changes)
update-major:
	@echo "WARNING: This will update to latest versions including major updates!"
	@echo "Press Ctrl+C to cancel or Enter to continue..."
	@read -s -n 1 key
	dotnet outdated -u --include-auto --latest

# Update a specific project
update-project:
	@if [ -z "$(PROJECT)" ]; then \
		echo "Usage: make update-project PROJECT=path/to/project.csproj"; \
		exit 1; \
	fi
	@echo "Updating packages in $(PROJECT)..."
	dotnet outdated -u --include-auto $(PROJECT)

# Update specific package across all projects
update-package:
	@if [ -z "$(PACKAGE)" ]; then \
		echo "Usage: make update-package PACKAGE=Package.Name"; \
		exit 1; \
	fi
	@echo "Updating $(PACKAGE) in all projects..."
	dotnet outdated -u --include-auto --package $(PACKAGE)

# Clean solution
clean:
	@echo "Cleaning solution..."
	dotnet clean
	rm -rf */bin */obj **/*/bin **/*/obj

# Restore packages
restore:
	@echo "Restoring packages..."
	dotnet restore

# Build solution
build:
	@echo "Building solution..."
	dotnet build --no-restore

# Run tests
test:
	@echo "Running tests..."
	dotnet test

# Complete update cycle
all: clean restore update build test
	@echo "Update cycle completed: cleaned, restored, updated packages, built, and tested"

# Update without breaking changes (only minor/patch versions)
safe-update:
	@echo "Performing safe update (minor/patch versions only)..."
	dotnet outdated -u --include-auto --version-lock major

# Generate report of outdated packages
report:
	@echo "Generating outdated packages report..."
	@dotnet outdated --format json > outdated-report.json
	@echo "Report saved to outdated-report.json"

# Update and show verbose output
update-verbose:
	@echo "Updating packages with verbose output..."
	dotnet outdated -u --include-auto --verbosity diag