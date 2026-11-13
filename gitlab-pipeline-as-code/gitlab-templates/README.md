# Reusable GitLab Templates

This directory is a separate Gitea repository: <http://localhost:3000/admin/gitlab-templates>. It contains shared GitLab pipeline definitions and hidden job templates, not application source or a GitLab orchestration project.

## Contents

- [pipelines/dotnet-ci.yml](pipelines/dotnet-ci.yml) composes fetch, build, test, and publish jobs and owns workflow rules, stages, defaults, and job dependencies.
- [templates/gitea-source.yml](templates/gitea-source.yml) provides `.gitea-source`, which validates the trigger payload and downloads the exact application commit into `application/`.
- [templates/dotnet-base.yml](templates/dotnet-base.yml) provides `.dotnet` with shared SDK image and NuGet cache settings.
- [templates/dotnet-build.yml](templates/dotnet-build.yml) provides `.dotnet-build` for Release builds and build artifacts.
- [templates/dotnet-test.yml](templates/dotnet-test.yml) provides `.dotnet-test` for test execution and TRX reports.
- [templates/dotnet-publish.yml](templates/dotnet-publish.yml) provides `.dotnet-publish` for application publish artifacts.

The source directory and artifact paths are part of the shared contract. `DOTNET_TEST_PROJECT` must reference the application project so the build produces both test and application assemblies. Images and source settings come from the consuming GitLab project's CI variables.

## Application Entry Point

The application repository keeps this small `.gitlab-ci.yml`:

```yaml
include:
  - remote: 'http://gitea:3000/admin/gitlab-templates/raw/branch/main/pipelines/dotnet-ci.yml'

variables:
  DOTNET_TEST_PROJECT: application/tests/Sample.Api.Tests.csproj
  DOTNET_APP_PROJECT: application/src/Sample.Api.csproj
```

GitLab loads the application's external YAML, resolves the shared pipeline and its source-fetch/build/test/publish templates, then merges application variables and any job overrides. Each .NET job template includes the common base, so a consumer can include an individual job file and use `extends` without manually including its base. The pipeline owns stages and dependencies; reusable templates own commands and artifact settings. Test and publish still expect the build outputs supplied by the consuming pipeline.

Required GitLab project variables are `GITEA_URL`, `GITEA_REPOSITORY`, `GITEA_BRANCH_REF`, `PYTHON_IMAGE`, and `DOTNET_IMAGE`. The project's trigger webhook and Runner setup are still required; an include does not create them for another application. The supplied pipeline accepts trigger-created runs only.

## Provision And Edit

Run `bash scripts/up.sh` from the parent sample directory. Bootstrap creates and seeds this repository before seeding or migrating the application entry point. Configure `gitea.templates_repository` and `gitea.templates_branch` in the parent bootstrap settings before first provisioning. Bootstrap substitutes configured catalog URLs into newly seeded YAML.

To edit the existing repository, clone it rather than initializing unrelated history:

```bash
git clone http://localhost:3000/admin/gitlab-templates.git
cd gitlab-templates
```

Edit the relevant files, review the changes, and commit and push them normally. Bootstrap does not overwrite a populated catalog branch. Subsequent checkout edits must be committed to Gitea explicitly; rerunning setup is not a synchronization operation.

The catalog has no application webhook. A catalog push does not start application pipelines. Push an application change or run the parent sample's simulation to exercise the updated shared configuration. Existing pipelines retain their configuration snapshot; a new pipeline resolves the includes again.

## Access And Versioning

Every remote include URL, including the shared base, must be reachable by the GitLab server without authentication. Nested remote includes have no project-variable context, so their URLs are explicit rather than built from CI variables. Both Gitea repositories are public in this trusted local lab.

The example follows `main`, so shared-template changes affect future application runs and are not pinned to the application's pushed SHA. Protect and review catalog changes as executable CI code. For production releases, publish a versioned catalog whose nested URLs also point to immutable revisions, and pin the application include to that release. Pinning only the outer file while its nested includes still follow `main` does not freeze the complete pipeline.

Official references: [remote includes](https://docs.gitlab.com/ci/yaml/#includeremote), [configuration merging](https://docs.gitlab.com/ci/yaml/includes/), and [extends](https://docs.gitlab.com/ci/yaml/#extends).
