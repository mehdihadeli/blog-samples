# Dotnet CI Orchestration Repository

This directory is a separate Gitea repository: `admin/dotnet-ci` on the
*application* instance. It demonstrates a separate, centralized CI entry
point; it does not contain application source or the reusable action
implementations.

## Responsibilities

- Orchestration repository: `admin/dotnet-ci` (CI instance, 3110).
- Application source: `forgejo/dotnet-sample` (Forgejo instance, 3010).
- Reusable workflow and actions: `admin/admin-gitea-templates` (CI instance,
  3110).

## How It Works

[.gitea/workflows/ci.yml](.gitea/workflows/ci.yml) is a thin
`workflow_dispatch` entry point that calls the reusable workflow
[.gitea/workflows/dotnet-ci.yaml](.gitea/workflows/dotnet-ci.yaml) in this same
repository. The reusable workflow clones the application source from the
Forgejo instance (3010) and runs the merged `dotnet-build-test` composite
action.

The reusable workflow lives here because Gitea resolves `workflow_call`
server-side on the same instance; it cannot be called across instances.

## Trigger The Pipeline

Open the `dotnet-ci` repository, choose **Actions**, select **Orchestrated CI**,
and run it from `main`.

Equivalent API call:

```text
POST /api/v1/repos/admin/dotnet-ci/actions/workflows/ci.yml/dispatches
Body: {"ref":"main"}
```

## Why Not `repository_dispatch`?

GitHub triggers a `repository_dispatch` event with a dedicated REST endpoint
(`POST /repos/{owner}/{repo}/dispatches`). Gitea Actions has no such endpoint
and does not support `repository_dispatch` as a workflow trigger, so the
orchestration repository uses `workflow_dispatch` only. The native
push-to-pipeline webhook is handled by the application repository's own
workflow.
