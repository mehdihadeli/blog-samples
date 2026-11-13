# Tekton .NET Build Catalog

Minimal Tekton resources for cloning and building .NET applications hosted in Gitea.

## Pipelines

| Pipeline | Purpose | Credentials |
| --- | --- | --- |
| [dotnet-ci](pipelines/dotnet-ci.yaml) | Clone a repository and build its .NET project | None for public source |

## Tasks

| Task | Contract |
| --- | --- |
| [dotnet-build](tasks/dotnet-build.yaml) | Configurable SDK/directory; Release build with warnings as errors |

The pipeline uses the pinned upstream Tekton Catalog `git-clone` task directly.

## Application Contract

The pipeline requires `repo-url` and `revision`. `project-directory` defaults to `src`, and `sdk-image` defaults to `mcr.microsoft.com/dotnet/sdk:10.0`.

| Workspace | Contents |
| --- | --- |
| `shared-workspace` | Per-run PVC; the cloned source is stored under `source/` |

## Publish and Resolve

Publish this directory as the Gitea `tekton-catalog` repository before application events reference it. PaC resolves the local `dotnet-build` task from the remote Pipeline repository and fetches the pinned upstream `git-clone` task.

See [application setup](../dotnet-tekton-sample/readme.md). The separate GitOps repository contains only runtime desired state.
<!-- end -->
