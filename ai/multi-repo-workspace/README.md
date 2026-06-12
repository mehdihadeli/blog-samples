# Multi-Repo Workspace Sample

This sample demonstrates the workspace layout and helper scripts from the article.

## What is here

- `workspace/WORKSPACE.md`
- `workspace/start-copilot.sh`
- `workspace/scripts/repo-status.sh`
- `workspace/scripts/repo-sync.sh`
- `tests/test-workspace.sh`

## Run the sample tests

```bash
bash ./tests/test-workspace.sh
```

The test creates temporary Git repositories, marks one repo dirty, runs the helper scripts, and validates the output.
