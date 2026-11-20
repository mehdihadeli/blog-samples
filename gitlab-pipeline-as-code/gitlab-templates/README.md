# Reusable GitLab Templates

This directory is a separate Gitea repository: <http://localhost:3000/admin/gitlab-templates>. It contains a shared GitLab pipeline component and four reusable job components, not application source or a GitLab orchestration project.

## Contents

- [pipelines/dotnet-ci.yml](pipelines/dotnet-ci.yml) is a pipeline component. It declares `spec:inputs`, owns workflow rules, stages, and defaults, and composes the four job components into one graph.
- [templates/git-clone.yml](templates/git-clone.yml) is a clone component modeled on the Tekton `git-clone` catalog task. It clones a repository URL into a subdirectory and publishes the resolved commit, URL, and committer date through a dotenv report.
- [templates/dotnet-build.yml](templates/dotnet-build.yml) is a build component that restores and builds a .NET project or solution.
- [templates/dotnet-test.yml](templates/dotnet-test.yml) is a test component that runs the prebuilt assembly and keeps TRX results even on failure.
- [templates/dotnet-publish.yml](templates/dotnet-publish.yml) is a publish component that writes the published output and records the source revision.
- [templates/security/](templates/security/) holds GitLab's official security scanners, mirrored from the public CI/CD catalog and consumed with `include: remote`.

Every file starts with a `spec:` header containing `spec:inputs` and references those values with `$[[ inputs.<name> ]]`. That is the CI/CD component format described in the official [CI/CD components documentation](https://docs.gitlab.com/ci/components/) and the [inputs documentation](https://docs.gitlab.com/ci/inputs/). Each job component is self-contained: it declares its own image, NuGet cache, and artifact paths, so a consumer can include one file on its own.

## Security Components

The five files under [templates/security/](templates/security/) are verbatim copies of GitLab's first-party security components from the public [CI/CD catalog](https://gitlab.com/explore/catalog). Only the dependency scanning file is renamed (from `templates/main/template.yml` to `dependency-scanning.yml`); the rest keep their upstream names. Each keeps its `spec:inputs` header and `$[[ inputs.* ]]` interpolation, so a consumer passes an `inputs:` block just like the other components in this catalog.

| Local file | Source component project | Source file |
| --- | --- | --- |
| [sast.yml](templates/security/sast.yml) | [gitlab.com/components/sast](https://gitlab.com/components/sast) | `templates/sast.yml` |
| [iac-sast.yml](templates/security/iac-sast.yml) | [gitlab.com/components/sast](https://gitlab.com/components/sast) | `templates/iac-sast.yml` |
| [secret-detection.yml](templates/security/secret-detection.yml) | [gitlab.com/components/secret-detection](https://gitlab.com/components/secret-detection) | `templates/secret-detection.yml` |
| [dependency-scanning.yml](templates/security/dependency-scanning.yml) | [gitlab.com/components/dependency-scanning](https://gitlab.com/components/dependency-scanning) | `templates/main/template.yml` (renamed) |
| [code-quality.yml](templates/security/code-quality.yml) | [gitlab.com/components/code-quality](https://gitlab.com/components/code-quality) | `templates/code-quality.yml` |

These are point-in-time snapshots of each component's `main` branch, not live references: `sast` and `iac-sast` from commit `732d5e0a`, `secret-detection` from `acfe5337`, `dependency-scanning` from `182e3b8`, and `code-quality` from `1f0f0028`. They do not track upstream, so a GitLab update does not reach this catalog until someone re-mirrors it.

Mirroring these files into Gitea lets the pipelines consume the security scanners with `include: remote` plus `inputs:` while the GitLab server never reaches the public internet to resolve them. The analyzers still pull their container images from the registry, and two of them (`dependency-scanning` and `code_quality`) still need external resources at run time, so mirroring the YAML does not remove those offline limitations.

## Image Entrypoints

GitLab Runner runs a job by passing its script to `sh -c` inside the image, but Docker applies the image's `ENTRYPOINT` first. `alpine/git` ships `ENTRYPOINT ["git"]`, so a plain `image: alpine/git:2.43.0` produces `git sh -c ...` and fails immediately with `git: 'sh' is not a git command`. The clone component therefore declares the image as a mapping that clears the entrypoint:

```yaml
  image:
    name: $[[ inputs.image ]]
    entrypoint: [""]
```

Keep that override when you change the `image` input. Images without an entrypoint, such as the .NET SDK image, do not need it and keep the short `image:` form.

## Application Entry Point

The application repository keeps this small `.gitlab-ci.yml`:

```yaml
include:
  - remote: 'http://gitea:3000/admin/gitlab-templates/raw/branch/main/pipelines/dotnet-ci.yml'
    inputs:
      source_url: $GIT_CLONE_URL
      source_revision: $GIT_CLONE_REVISION
```

GitLab loads the application's external YAML, interpolates the pipeline component's inputs, and then resolves the nested component includes. Unset inputs fall back to their `spec:inputs` defaults, so the application only supplies the two values that are specific to a push: the repository URL and the revision to build.

Stage ordering replaces `needs`. `fetch`, `build`, `test`, and `publish` run in sequence, and every stage downloads the artifacts of the stages before it, so the clone job's dotenv report reaches publish as `GIT_CLONE_COMMIT`. A pipeline that needs different ordering can pass a different `stage` input per job component, or add `needs` in a wrapper job of its own.

Required GitLab project variables are `GIT_CLONE_URL` and `GIT_CLONE_REVISION`. The clone component clones whatever URL and revision the pipeline supplies, so it is not tied to Gitea or to a webhook payload. The project's trigger webhook and Runner setup are still required; an include does not create them for another application. The supplied pipeline accepts trigger-created runs only.

## Components Hosted Outside GitLab

A released catalog component is normally consumed with `include:component` against a GitLab project. This sample deliberately keeps pipeline YAML in Gitea, so the components are consumed with `include:remote` plus [`include:inputs`](https://docs.gitlab.com/ci/yaml/#includeinputs) instead. The authoring format is identical: the same `spec:` header, the same `$[[ inputs.* ]]` interpolation, and the same typed defaults. What changes is only how the consumer reaches the file, because `include:component` resolves component addresses on the GitLab instance.

If you move this catalog into a GitLab project, publish each file under `templates/` with the `release` keyword and switch the consumer entry point to `include:component`. The job files do not change.

## Create And Edit

Create an empty `gitlab-templates` repository in Gitea as the `admin` user, then push this directory's contents. The parent [README.md](../README.md) has the full command sequence, including the `git clone` into a scratch directory and the `cp` of `pipelines/`, `templates/`, and this file.

To edit the existing repository, clone it rather than initializing unrelated history:

```bash
git clone http://localhost:3000/admin/gitlab-templates.git
cd gitlab-templates
```

Edit the relevant files, review the changes, and commit and push them normally. This checkout is not a synchronization source: changes must be committed to Gitea explicitly. No setup script overwrites or migrates a populated catalog branch.

The catalog has no application webhook. A catalog push does not start application pipelines. Push an application change to exercise the updated shared configuration. Existing pipelines retain their configuration snapshot; a new pipeline resolves the includes again.

## Access And Versioning

Every remote include URL must be reachable by the GitLab server without authentication. Nested remote includes have no project-variable context, so their URLs are explicit rather than built from CI variables. Both Gitea repositories are public in this trusted local lab.

The example follows `main`, so shared-template changes affect future application runs and are not pinned to the application's pushed SHA. Protect and review catalog changes as executable CI code. For production releases, publish a versioned catalog whose nested URLs also point to immutable revisions, and pin the application include to that release. Pinning only the outer file while its nested includes still follow `main` does not freeze the complete pipeline.

Official references: [CI/CD components](https://docs.gitlab.com/ci/components/), [CI/CD inputs](https://docs.gitlab.com/ci/inputs/), [remote includes](https://docs.gitlab.com/ci/yaml/#includeremote), and [configuration merging](https://docs.gitlab.com/ci/yaml/includes/).
