# Gitea Application With Local GitLab CI

Keep application source and a small pipeline entry point in Gitea, with reusable pipelines and jobs in a separate `gitlab-templates` repository. GitLab resolves the YAML and executes CI through GitLab Runner's Docker executor. Everything runs in Docker Compose; Kubernetes, Tekton, Gitea Actions, repository mirroring, and a webhook relay are not required.

## Requirements

- Docker Desktop using Linux containers, or Docker Engine with Compose v2.
- Git Bash on Windows; Bash on Linux or macOS.
- Approximately 8 GB of available Docker memory for GitLab and build jobs.
- Internet access on the first run for Alpine Git packages, NuGet packages, and the Runner helper image. Existing server, Runner, Python, and .NET images are reused.

The image tags match the images available when this sample was created. Review and update them together for another environment. This is a local development setup, not a production deployment.

## Start

Run from this directory:

```bash
bash scripts/up.sh
bash scripts/simulate.sh
```

The first command waits up to 20 minutes for GitLab initialization. It automatically creates admin accounts, repositories, CI variables, a pipeline trigger token, a Gitea push webhook, and a project-scoped Docker Runner. No UI setup or manually copied tokens are needed.

The second command makes a real Git commit and pushes it to Gitea. It never directly calls GitLab's trigger API. It waits for the webhook-created pipeline, verifies artifacts and test results, starts the published application, and checks its HTTP responses.

Default browser endpoints and local-only credentials:

| Service | URL | Username | Password |
| --- | --- | --- | --- |
| Gitea | <http://localhost:3000> | `admin` | `GiteaLocal123!` |
| GitLab | <http://localhost:8929> | `root` | `LocalCi!9rF7p2Q` |
| Published application | <http://localhost:5080/weatherforecast> | None | None |

Application repository: <http://localhost:3000/admin/dotnet-gitlab-sample>

Reusable templates repository: <http://localhost:3000/admin/gitlab-templates>

CI project and pipelines: <http://localhost:8929/root/dotnet-ci/-/pipelines>

The application endpoint is available after a successful simulation, not merely after starting the servers.

## Configuration

- [docker-compose.yaml](docker-compose.yaml): images, ports, services, networks, volumes, and health checks.
- [.env.example](.env.example): optional environment overrides. Docker Compose also loads an untracked `.env` in this directory.
- [deployments/gitea/app.ini](deployments/gitea/app.ini): Gitea installation settings and webhook host allowlist.
- [deployments/gitea/bootstrap-admin.sh](deployments/gitea/bootstrap-admin.sh): database migration and creation of the configured Gitea administrator.
- [deployments/gitlab/gitlab.rb](deployments/gitlab/gitlab.rb): GitLab URL, initial root password, and reduced local resource settings.
- [deployments/gitlab/seed.rb](deployments/gitlab/seed.rb): root email, disabled signups, Gitea outbound allowlist entry, missing CI signing-key initialization, and a seven-day bootstrap API token stored in a private Docker volume.
- [deployments/bootstrap/settings.json](deployments/bootstrap/settings.json): project names, Gitea branch, GitLab namespace, Runner tags, and Runner network.
- [deployments/bootstrap/provision.py](deployments/bootstrap/provision.py): API-based project, webhook, and Runner provisioning.
- [application/.gitlab-ci.yml](application/.gitlab-ci.yml): application entry point, including the shared pipeline and defining test/application project paths.
- [gitlab-templates/](gitlab-templates/): separate Gitea repository with the composed .NET pipeline, reusable source-fetch/.NET jobs, and reuse instructions.
- [dotnet-ci/README.md](dotnet-ci/README.md): README-only GitLab project contents and instructions for pushing them to `root/dotnet-ci`.

GitLab does not have a supported `gitlab.rb` setting for creating arbitrary projects. The sample combines native server configuration with a one-shot, repeatable API bootstrap instead.

Set passwords before the first startup. Changing the environment later does not reset existing users' passwords. The Gitea admin username and GitLab namespace should also be chosen before first provisioning. The sample creates the personal `root` namespace by default; another namespace must already exist.

Ports can be overridden with `GITEA_HTTP_PORT`, `GITEA_SSH_PORT`, `GITLAB_HTTP_PORT`, and `APP_HTTP_PORT`. Internal service ports remain fixed. Default Gitea SSH port is `22222` to avoid a Windows-reserved `2222` port encountered during validation.

## Repository Ownership

The Gitea `admin/dotnet-gitlab-sample` repository contains the application, tests, Dockerfile, and a small `.gitlab-ci.yml`. The separate `admin/gitlab-templates` repository owns `pipelines/dotnet-ci.yml` and the hidden jobs under `templates/`. Both repositories are intentionally public on this local instance so GitLab can retrieve remote YAML without authentication and the fetch job needs no source-access secret.

The application entry point includes the shared pipeline and supplies its project paths:

```yaml
include:
  - remote: 'http://gitea:3000/admin/gitlab-templates/raw/branch/main/pipelines/dotnet-ci.yml'

variables:
  DOTNET_TEST_PROJECT: application/tests/Sample.Api.Tests.csproj
  DOTNET_APP_PROJECT: application/src/Sample.Api.csproj
```

The shared pipeline includes separate source-fetch, build, test, and publish templates, then composes their hidden jobs using `extends`. Each .NET job file includes [templates/dotnet-base.yml](gitlab-templates/templates/dotnet-base.yml) for common SDK image and cache settings. Nested remote includes use explicit URLs because GitLab resolves them without project-variable context. See [gitlab-templates/README.md](gitlab-templates/README.md) for reuse, publishing, and versioning guidance.

The private GitLab `root/dotnet-ci` project remains the orchestration project. Its local project contents live under [dotnet-ci/](dotnet-ci/), with a README explaining how to publish them to GitLab. Its repository contains only README; its project settings retain CI variables, trigger credentials, Runner assignment, pipeline history, logs, and artifacts. Do not delete the project. Its **Settings > CI/CD > General pipelines > CI/CD configuration file** points to:

```text
http://gitea:3000/admin/dotnet-gitlab-sample/raw/branch/main/.gitlab-ci.yml
```

Bootstrap seeds [gitlab-templates/](gitlab-templates/) first and [application/](application/) only when their Gitea branches are empty. It adds a missing application entry point or migrates the exact previous monolithic sample YAML, identified by its normalized SHA-256. Customized application YAML is left unchanged with a review message; manually adopt the shared include if needed. Bootstrap then checks that GitLab can fetch and lint the include chain, sets the external configuration URL, and removes obsolete YAML from GitLab. It never force-pushes or replaces an application branch.

Rerunning `up.sh` reuses both Gitea repositories, the GitLab project, the application webhook, and the Runner. Apart from the guarded legacy migration, it does not overwrite existing Gitea YAML from this checkout; commit later changes directly to the appropriate Gitea repository. Configure `gitea.templates_repository` and `gitea.templates_branch` before first setup if their defaults should differ. Generated authentication material stays in named Docker volumes, not in this checkout. GitLab Runner's configuration is kept between restarts so Runner-managed token updates are not overwritten.

Startup also reapplies Gitea configuration and recreates its container while preserving its data. After editing GitLab's mounted configuration, run `docker compose restart gitlab` before rerunning `up.sh` so GitLab reconfigures itself.

## Push And Pipeline Flow

1. A developer pushes to Gitea's configured application branch, `main` by default.
2. Gitea sends a JSON push webhook to `http://gitlab:8929/api/v4/projects/<id>/ref/main/trigger/pipeline?token=<generated-token>`.
3. GitLab resolves the CI project's `main` ref, loads the application entry point, and resolves the shared pipeline, individual job templates, and common .NET base from `gitlab-templates`. The pipeline has `CI_PIPELINE_SOURCE=trigger`.
4. `fetch-source` reads the file named by `TRIGGER_PAYLOAD`, validates the repository, CI configuration ref, and `after` SHA, and downloads that exact Gitea commit as a source archive.
5. `build` restores and builds the application and test project in the .NET SDK image.
6. `test` runs three xUnit tests and retains the TRX result file.
7. `publish` publishes the previously built application and retains it with `source.json`.

The webhook URL's `main` is the GitLab orchestration branch. Gitea's application branch is independently configured. `CI_COMMIT_SHA` identifies GitLab's README-only project commit, not the application or external YAML revision. `CI_CONFIG_PATH` identifies the external URL, while `source.json` records the exact Gitea application revision.

The application and template URLs follow branches, so GitLab fetches their latest YAML when creating a pipeline. Concurrent pushes can therefore execute an older application commit with newer pipeline YAML or templates. The application archive remains pinned to the webhook's exact `after` SHA. This sample does not provide exact-commit YAML provenance; that would require additional orchestration to select an immutable configuration per event. Pin the entire include chain for immutable template releases, not only the outer pipeline. The template repository has no webhook: template changes affect subsequent application runs but do not trigger one automatically.

GitLab serializes merged trigger parameters, not the untouched webhook body: its URL ref replaces Gitea's original `ref`. Gitea's webhook branch filter enforces the application branch. The `ref` in `source.json` records that configured branch, not an independently verified original payload ref. Repository identity and the exact `after` commit SHA are checked in the job.

Every job uses `GIT_STRATEGY: empty`; Runner does not try to clone the CI repository or the application automatically. Artifacts carry the fetched source and built output between jobs. NuGet packages are cached by Runner.

Runner-created job containers explicitly join the `local-gitlab-ci` Docker network, enabling `gitlab:8929` and `gitea:3000` resolution. Browser links use localhost; internal traffic never uses localhost to reach another container.

The Runner image manages jobs. It is not the application build image. Python handles source retrieval, and the .NET SDK image handles .NET stages. No Docker-in-Docker or privileged build containers are used. The application Dockerfile is included for independent image builds, but this pipeline publishes application binaries rather than building or pushing an application image.

## Verification Evidence

Verified locally on 2026-11-13: [pipeline #13](http://localhost:8929/root/dotnet-ci/-/pipelines/13) was triggered by a real application push to Gitea and succeeded with all four jobs and three passing tests. The built Gitea revision was `1af6d3d67d1c450857609ec98ebbafbd4acbc30b`. GitLab's remote lint resolved all six files in the separate `admin/gitlab-templates` repository: the shared pipeline, source-fetch template, common .NET base, and individual build/test/publish templates. Jobs `41` through `44` passed. The verifier confirmed GitLab's orchestration commit had no `.gitlab-ci.yml`, the fetch job reported the external configuration URL, and published artifacts matched the pushed application SHA. The published container passed both HTTP checks.

Successful simulation writes ignored local files under `evidence/`:

- `result.json`: pipeline ID and URL, trigger source, Gitea SHA, GitLab project SHA, external configuration URL, template repository and resolved template URLs, confirmation that GitLab has no pipeline YAML, four job results, and test count.
- `source.json`: application repository, branch ref, and exact Gitea revision.
- `TestResults/tests.trx`: three passing tests, checked by parsing its counters.
- `test-artifacts.zip` and `publish-artifacts.zip`: original GitLab job artifacts.
- `publish/`: published application used by the `sample-app` container.
- `runtime.json`: verified health and forecast HTTP responses.

Tests are `Should_ReturnForecastValues_When_ForecastRequested`, `Should_ReturnHealthy_When_HealthRequested`, and `Should_ReturnNotFound_When_UnknownEndpointRequested`.

Run `bash scripts/simulate.sh` again for another independent pushed commit and pipeline. Do not run simulations concurrently; they share the local evidence directory and published-app container.

## Security Boundaries

Default passwords are deliberately documented for this disposable local lab. Do not reuse them outside it. All published ports bind to `127.0.0.1`; internal HTTP traffic is unencrypted.

GitLab's outbound local-request allowlist includes only the configured Gitea host and port (`gitea:3000` by default), preserving any existing entries. This permits server-side YAML retrieval without enabling unrestricted local-network access. The external URL and every nested template URL require unauthenticated access; private repositories would need a different configuration delivery design. Anyone able to change application YAML or shared templates can run jobs with this project's CI permissions, so write access to either repository is a CI security boundary.

Runner has access to the host Docker socket and therefore effective control over the Docker host. Only run trusted jobs here. The socket is not mounted into build containers, and jobs run without privileged mode.

The GitLab trigger token authenticates webhook delivery. GitLab's trigger endpoint does not validate Gitea HMAC signatures. Add a signature-validating relay before exposing this integration outside a trusted local network. Restrict access to Gitea webhook settings, which contain the trigger URL.

Pipeline status and logs are visible in GitLab. This minimal flow does not post commit statuses back to Gitea or implement Gitea pull-request checks. Those are separate integrations, not automatic effects of sending a webhook.

## Stop And Troubleshoot

```bash
docker compose ps
docker compose logs --tail 80 gitlab gitea runner
docker compose stop
```

Stopping preserves repositories, accounts, Runner registration, and artifacts. To restart after stopping, use `bash scripts/up.sh`.

Inspect Gitea repository **Settings > Webhooks > Recent Deliveries** if no pipeline appears. The target host must remain on Gitea's webhook allowlist. Inspect GitLab **Settings > CI/CD > Runners** if jobs stay pending. The Runner requires the `docker` tag.

If GitLab cannot load external configuration, check the project's **CI/CD configuration file** URL, the presence of `.gitlab-ci.yml` in Gitea, and GitLab **Admin > Settings > Network > Outbound requests**. Rerunning `up.sh` restores the Gitea host-and-port allowlist entry. Bootstrap lints the remote YAML and fails before removing the legacy GitLab file if retrieval or validation fails. Edit external YAML in Gitea; GitLab's pipeline editor cannot edit it.

Gitea 28 uses `[security] ALLOWED_HOST_LIST`, not the old webhook-section setting. This sample permits `gitlab:8929` and private addresses on port `8929`; the explicit private-address entry is necessary for Docker-network delivery.

The local Puma process has a 3 GB watchdog budget. The default 1.5 GB limit caused repeated restarts and HTTP 502 responses in this environment. If Runner trace uploads return HTTP 500 with `CI job token signing key is not set`, rerun `up.sh`; its guarded seed initializes missing signing keys without replacing existing ones.

The first Docker job can take longer while Runner downloads its version-matched helper image. NuGet restore also needs external access on the first run. Build jobs use `if-not-present` image pulling to reuse local images.

References: [GitLab external CI configuration](https://docs.gitlab.com/ci/pipelines/settings/#specify-a-custom-cicd-configuration-file), [outbound request filtering](https://docs.gitlab.com/security/webhooks/), [pipeline triggers](https://docs.gitlab.com/ci/triggers/), [Docker executor](https://docs.gitlab.com/runner/executors/docker/), [Runner creation API](https://docs.gitlab.com/api/users/#create-a-runner-linked-to-a-user), and [Gitea webhooks](https://docs.gitea.com/usage/webhooks/).
