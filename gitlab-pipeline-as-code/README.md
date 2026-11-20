# Gitea Application With Local GitLab CI

Keep application source and a small pipeline entry point in Gitea, with reusable pipelines and jobs in a separate `gitlab-templates` repository. GitLab resolves the YAML and executes CI through GitLab Runner's Docker executor. Docker Compose starts the servers; the GitLab and Gitea configuration is done once, manually, through their web interfaces.

There is no Python provisioning image, no Rails bootstrap script, and no separate provisioning container. On a fresh Gitea volume, Compose creates the documented local `admin` / `admin` account after Gitea becomes ready. Kubernetes, Tekton, Gitea Actions, repository mirroring, and a webhook relay are not required.

## Requirements

- Docker Desktop using Linux containers, or Docker Engine with Compose v2.
- Git Bash on Windows; Bash on Linux or macOS.
- Approximately 8 GB of available Docker memory for GitLab and build jobs.
- Internet access on the first run for NuGet packages and the Runner helper image. Server, Runner, and .NET images are pulled once and reused.

The image tags in [deployments/docker-compose.yaml](deployments/docker-compose.yaml) match the images available when this sample was created. They are pinned in that one file and are not duplicated anywhere else, so review and update them there. This is a local development setup, not a production deployment.

## Start The Servers

Run every `docker compose` command in this file from the `deployments/` directory, where `docker-compose.yaml` lives:

```bash
docker compose up -d --wait gitlab gitea
```

No `.env` file is needed. Every value the file needs is either pinned directly in `docker-compose.yaml` or has an inline default, so the command is reproducible from a clean checkout.

The first GitLab start takes several minutes. Gitea starts in seconds. Check later with:

```bash
docker compose ps
```

| Service | Browser URL |
| --- | --- |
| Gitea | <http://localhost:3000> |
| GitLab | <http://localhost:8929> |
| Published application (after verification) | <http://localhost:5080/weatherforecast> |

The `runner` and `sample-app` services are not started by the command above. Register the Runner first, then start it; start `sample-app` only when you have downloaded publish artifacts.

### Setup Screenshots

These screenshots were captured from the running local services after the Compose health checks passed:

![GitLab sign-in page](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-sign-in.png)

![Gitea sign-in page](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitea-sign-in.png)

## Get The GitLab Root Password

Compose does not set a GitLab password. On first boot GitLab generates one and writes it inside the container:

```bash
docker compose exec gitlab cat /etc/gitlab/initial_root_password
```

Use the username `root` with that password. The file is removed automatically after 24 hours, or after the password is changed.

If the file is gone, reset the password from inside the container instead of adding an environment variable to Compose:

```bash
docker compose exec gitlab gitlab-rake "gitlab:password:reset[root]"
```

The rake task prompts for the new password twice. For a scripted reset with a non-interactive shell:

```bash
docker compose exec gitlab gitlab-rails runner \
  "u=User.find_by_username('root'); u.password='Change-Me-9#Xy2z'; u.password_confirmation='Change-Me-9#Xy2z'; u.save!"
```

Use a strong, unique password; GitLab rejects weak or commonly used combinations. Avoid an exclamation mark inside double quotes in Git Bash, where it triggers history expansion.

A container reset changes the database only. It does not write a password into this checkout or into Compose.

## Sign In To Gitea

Self-registration is disabled. On a fresh `gitea-data` volume, the Compose entrypoint waits for Gitea readiness and creates the local administrator automatically:

```text
username: admin
password: admin
email: admin@example.test
```

Sign in at <http://localhost:3000>. The bootstrap is idempotent: restarting the service does not replace the account or its password. The default is intentionally weak and exists only for this disposable local lab.

Gitea rejects passwords shorter than six characters by default. [deployments/gitea/app.ini](deployments/gitea/app.ini) sets `MIN_PASSWORD_LENGTH = 5` under `[security]` so the documented default works. That is a local-lab concession, not a recommendation.

The Compose service copies that INI into the `gitea-data` volume only when no runtime configuration exists. Gitea rewrites its runtime `app.ini` on every start (it stores generated tokens there), which is why the tracked file is mounted read-only and used only to seed the volume:

```yaml
entrypoint:
  - /bin/sh
  - -c
  - |
    set -eu
    mkdir -p /data/gitea/conf
    test -f /data/gitea/conf/app.ini || cp /config/app.ini /data/gitea/conf/app.ini
    /usr/bin/entrypoint &
    gitea_pid=$$!
    trap 'kill "$$gitea_pid" 2>/dev/null || true; wait "$$gitea_pid" 2>/dev/null || true' INT TERM
    until wget -q --spider http://127.0.0.1:3000/api/healthz; do
      if ! kill -0 "$$gitea_pid" 2>/dev/null; then
        wait "$$gitea_pid"
        exit 1
      fi
      sleep 2
    done
    if ! su-exec git /usr/local/bin/gitea --config /data/gitea/conf/app.ini admin user list | grep -Eq '^[[:space:]]*[0-9]+[[:space:]]+admin[[:space:]]'; then
      su-exec git /usr/local/bin/gitea --config /data/gitea/conf/app.ini admin user create \
        --username admin --password admin \
        --email admin@example.test --admin --must-change-password=false
    fi
    wait "$$gitea_pid"
```

Ports and the webhook egress allowlist are environment overrides, so they follow the Compose port variables on every start without editing the INI:

```yaml
GITEA__server__ROOT_URL: http://localhost:${GITEA_HTTP_PORT:-3000}/
GITEA__server__SSH_PORT: ${GITEA_SSH_PORT:-22222}
GITEA__security__ALLOWED_HOST_LIST: gitlab:${GITLAB_HTTP_PORT:-8929}, private:${GITLAB_HTTP_PORT:-8929}
```

Gitea 28 reads the egress allowlist from `[security]`, not the legacy `[webhook]` section. The sample permits the GitLab hostname and private addresses on the GitLab port only. See the [Gitea configuration reference](https://docs.gitea.com/administration/config-cheat-sheet/#security-security) when upgrading.

## Change Passwords And Enable Two-Factor Authentication

Do this after the first sign-in, even in a disposable lab.

### GitLab

1. Sign in at <http://localhost:8929> as `root` with the generated password from the container.
2. Open the avatar menu and choose **Edit profile**.
3. Select **Password**, enter the generated password as the current password, enter a strong new password twice, and save it.
4. Return to **Edit profile > Account > Two-factor authentication**.
5. Choose an authenticator-app method, scan the QR code with an authenticator app, enter the current one-time code, and confirm.
6. Download or copy the recovery codes and store them offline. GitLab requests the one-time code at the next sign-in.

If the generated password file has expired or the password is forgotten, reset it from the GitLab container:

```bash
docker compose exec gitlab gitlab-rails runner \
  "u=User.find_by_username('root'); u.password='Change-Me-9#Xy2z'; u.password_confirmation='Change-Me-9#Xy2z'; u.save!"
```

Use a unique password that satisfies GitLab's password policy. This changes the database only; it does not write credentials to Compose.

### Gitea

1. Sign in at <http://localhost:3000> with `admin` / `admin`.
2. Open the avatar menu and choose **Settings > Security**.
3. Change the password, save it, and sign in again with the new value.
4. On the same page, enable **Two-factor authentication**, scan the QR code, confirm the authenticator code, and save the recovery codes offline.

Neither service stores these passwords in this checkout. Do not add passwords to Compose to avoid the change; reset through the container commands above when needed.

## Create The Gitea Repositories

Sign in at <http://localhost:3000> as `admin` and create two empty repositories owned by `admin`, without a README, license, or `.gitignore`:

- `dotnet-gitlab-sample`: application source and pipeline entry point.
- `gitlab-templates`: reusable pipeline and job templates.

Push the tracked contents from a scratch checkout. Replace `$SAMPLE` with the absolute path of this directory:

```bash
export SAMPLE=/path/to/gitlab-pipeline-as-code
cd "$(mktemp -d)"

git clone http://localhost:3000/admin/gitlab-templates.git
cp -r "$SAMPLE/gitlab-templates/pipelines" "$SAMPLE/gitlab-templates/templates" "$SAMPLE/gitlab-templates/README.md" gitlab-templates/
cd gitlab-templates
git add .
git commit -m "Add reusable GitLab templates"
git push origin main
cd ..

git clone http://localhost:3000/admin/dotnet-gitlab-sample.git
cp -r "$SAMPLE/application/src" "$SAMPLE/application/tests" "$SAMPLE/application/Dockerfile" "$SAMPLE/application/.gitlab-ci.yml" dotnet-gitlab-sample/
cd dotnet-gitlab-sample
git add .
git commit -m "Add sample application and pipeline entry point"
git push origin main
```

Git asks for credentials; use the Gitea administrator account or a Gitea access token. Copying selected paths instead of initializing Git inside the sample tree keeps this repository free of nested Git repositories, and it leaves local `bin/` and `obj/` output out of Gitea.

Both repositories must stay public on this local instance: GitLab retrieves the remote YAML and the clone job clones the source without a repository credential.

## Create The GitLab Group

Put both orchestration projects in one group so a single group Runner can serve them:

1. Sign in to <http://localhost:8929> as `root`.
2. Choose **Groups > New group > Create group**.
3. Set **Group name** to `Gitea .NET CI` and **Group URL** to `gitea-dotnet`, keep **Private**, and create the group.

![GitLab group with both orchestration projects](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-group.png)

## Create The GitLab Orchestration Project

1. Sign in to <http://localhost:8929> as `root`.
2. Choose **New project > Create blank project**.
3. Set the project name to `dotnet-ci`, select the `gitea-dotnet` group as the namespace, select **Private**, and enable **Initialize repository with a README**.
4. Create the project.

The project is required even though its Git repository holds no pipeline YAML. It owns CI variables, the trigger credential, Runner assignment, pipeline history, logs, and artifacts. Optional: publish the local [dotnet-ci/README.md](dotnet-ci/README.md) as project documentation, following that file's instructions.

## Configure GitLab For External CI

### Allow the Gitea host

GitLab refuses server-side requests to private addresses by default. As `root`, open **Admin > Settings > Network** and add `gitea:3000` to **Outbound requests > Allow requests to the local network from webhooks and integrations**. Keep the entry as host and port; do not allow an entire subnet.

![GitLab outbound requests allowlist](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-outbound-requests.png)

This allowlist and the Gitea webhook allowlist are independent. Gitea needs permission to reach GitLab; GitLab needs permission to reach Gitea.

### Point the project at Gitea YAML

1. Open the `dotnet-ci` project.
2. Go to **Settings > CI/CD** and expand **General pipelines**.
3. Set **CI/CD configuration file** to:

```text
http://gitea:3000/admin/dotnet-gitlab-sample/raw/branch/main/.gitlab-ci.yml
```

4. Select **Save changes**.

The URL uses the Compose service name because the GitLab server retrieves the file. The file must end with `.yml`. Do not copy pipeline YAML into the GitLab repository.

![GitLab CI/CD configuration file](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-ci-config-file.png)

### Add the CI variables

In the same project, open **Settings > CI/CD > Variables** and add these two variables, both with **Protect variable** and **Mask variable** disabled:

| Key | Value |
| --- | --- |
| `GIT_CLONE_URL` | `http://gitea:3000/admin/dotnet-gitlab-sample.git` |
| `GIT_CLONE_REVISION` | `refs/heads/main` |

These values fix the source repository and the configured branch outside the incoming event payload. The generic clone component clones whatever URL and revision the pipeline supplies; it never trusts a payload-supplied URL. The .NET SDK image is no longer a project variable: each job component builds its image name from the `dotnet_version` input.

![GitLab CI/CD variables](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-ci-variables.png)

### Create the pipeline trigger token

1. Open **Settings > CI/CD > Pipeline trigger tokens**.
2. Choose **Add new token**, set the description to `gitea push`, and create it.
3. Copy the token. You can reopen this page later to see it again.

The webhook target keeps the token in its query string. Keep the Gitea webhook settings private.

![GitLab pipeline trigger tokens](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-trigger-tokens.png)

### Create the group Runner

Both projects share one Runner, so register it once at the group level instead of per project:

1. Open the `gitea-dotnet` group, then **Build > Runners**.
2. Choose **New group runner**.
3. Set tags `docker,dotnet`, leave **Run untagged jobs** disabled, and create the runner.
4. Copy the authentication token shown once.

![GitLab group runners](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-group-runners.png)

## Register And Start The Runner

Register the Runner into the `runner-config` volume, then start the service:

```bash
docker compose run --rm --no-deps runner register \
  --non-interactive \
  --url http://gitlab:8929 \
  --token <RUNNER_AUTHENTICATION_TOKEN> \
  --name gitea-dotnet-group-runner \
  --executor docker \
  --docker-image mcr.microsoft.com/dotnet/sdk:10.0-alpine \
  --docker-network-mode local-gitlab-ci \
  --docker-pull-policy if-not-present \
  --docker-volumes /cache

docker compose up -d runner
```

`--docker-network-mode local-gitlab-ci` is required: Runner creates job containers outside Compose's service list, and they must resolve `gitlab` and `gitea`. `--docker-pull-policy if-not-present` reuses local images. The Runner container keeps the host Docker socket so it can create jobs; jobs themselves are unprivileged and do not receive the socket.

A single Runner defaults to `concurrent = 1`, so the scan stage's five security jobs run one at a time. Raise it so they run in parallel:

```bash
docker compose exec runner sed -i 's/^concurrent = .*/concurrent = 4/' /etc/gitlab-runner/config.toml
docker compose restart runner
```

The registration token is not stored in this checkout. It lives in the `runner-config` volume. Rerun the registration command only when you create a new Runner in GitLab.

## Connect Gitea Pushes To GitLab

1. Open the Gitea repository `admin/dotnet-gitlab-sample`.
2. Go to **Settings > Webhooks** and choose **Add webhook > Gitea**.
3. Set **Target URL** to:

```text
http://gitlab:8929/api/v4/projects/<PROJECT_ID>/ref/main/trigger/pipeline?token=<TRIGGER_TOKEN>
```

4. Set **HTTP method** to `POST` and **POST content type** to `application/json`.
5. Under **Trigger On**, enable **Push events** and set the branch filter to `main`.
6. Add the webhook, then use **Test delivery** or the **Recent Deliveries** tab to confirm a `200` response.

`<PROJECT_ID>` is the number shown on the `dotnet-ci` project overview page. `<TRIGGER_TOKEN>` is the token from the previous section. This URL starts a pipeline for GitLab's `main` ref, regardless of the Gitea branch in the payload; the Gitea branch filter and the configured `GIT_CLONE_REVISION` define the accepted source.

GitLab's trigger endpoint does not validate Gitea's HMAC signature. It authenticates the request with the trigger token only, so treat the webhook URL as a secret and keep this integration on a trusted local network.

## Run A Real Push

Clone the application repository, make a commit, and push. The push, not a direct API call, must create the pipeline:

```bash
git clone http://localhost:3000/admin/dotnet-gitlab-sample.git
cd dotnet-gitlab-sample
printf 'triggered at %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > pipeline-trigger.txt
git add pipeline-trigger.txt
git commit -m "Verify Gitea push triggers GitLab CI"
git push origin main
```

Open **CI/CD > Pipelines** in the `dotnet-ci` project. The new pipeline has source `trigger` and runs four jobs, one per stage:

1. `git-clone` clones the configured repository revision into `application/`.
2. `dotnet-build` restores and builds the solution in the .NET SDK image.
3. `dotnet-test` runs the three xUnit tests and keeps the TRX report.
4. `dotnet-publish` publishes the application and keeps `publish/`.

The job names come from the component files, not from the pipeline. Each component declares a `job-name` or fixed job name, so a consumer that needs a different name passes the input instead of renaming the file.

This flow was verified on GitLab 19.5.5 with Runner 19.5.3. Two failures are worth knowing about because neither shows up in a YAML lint:

- `git: 'sh' is not a git command` means the image has its own `ENTRYPOINT`. `alpine/git` ships `ENTRYPOINT ["git"]`, and Runner's script becomes an argument to git. The clone component declares `image: {name: ..., entrypoint: [""]}` to clear it.
- `GIT_CLONE_URL: parameter not set` means the project variable is missing. The clone script runs under `set -eu`, so an unset variable fails fast instead of cloning an empty URL.

## Add A Second Pipeline With GitLab Security Scanners

The shared catalog also drives a second entry point that mixes the Gitea-hosted reusable jobs with GitLab's built-in security scanners. It is documented here rather than provisioned by a script: create the second GitLab project by hand, following the same UI steps as the first, and point it at [application/.gitlab-ci2.yml](application/.gitlab-ci2.yml).

That file already lives in the Gitea application repository. Unlike `.gitlab-ci.yml`, which includes the single `pipelines/dotnet-ci.yml` component, this file composes the pipeline directly from two sources:

- The four Gitea job templates from `gitlab-templates`: `git-clone`, `dotnet-build`, `dotnet-test`, and `dotnet-publish`, consumed with `include: remote`.
- GitLab's five security scanners, mirrored from the public CI/CD catalog into `gitlab-templates` under `templates/security/` and consumed with `include: remote` plus `inputs:`.

Those security files are verbatim copies of the GitLab.com CI/CD catalog components (`gitlab.com/components/sast`, `secret-detection`, `dependency-scanning`, and `code-quality`). Mirroring them into Gitea keeps their `spec:inputs` header and `$[[ inputs.* ]]` interpolation while the GitLab server never reaches the public internet to resolve a scanner. See [gitlab-templates/README.md](gitlab-templates/README.md) for the per-file provenance. Container scanning is omitted because the pipeline publishes .NET binaries and this lab disables the GitLab container registry; it needs a built image to scan.

### Create The Second Orchestration Project

Repeat the steps in [Create The GitLab Orchestration Project](#create-the-gitlab-orchestration-project) with `dotnet-ci2` as the project name, placing it in the same `gitea-dotnet` group. Then configure the new project by hand:

1. Open **Settings > CI/CD > General pipelines** and set **CI/CD configuration file** to:

```text
http://gitea:3000/admin/dotnet-gitlab-sample/raw/branch/main/.gitlab-ci2.yml
```

2. Under **Variables**, add the same two variables as the first project, `GIT_CLONE_URL` and `GIT_CLONE_REVISION`, both with **Protect variable** and **Mask variable** disabled.
3. Under **Pipeline trigger tokens**, add a token with the description `gitea push dotnet-ci2` and copy it.

No new Runner is needed. `dotnet-ci2` lives in the same `gitea-dotnet` group, so the group Runner registered earlier already serves it.

### Connect A Second Gitea Webhook

Open the Gitea repository `admin/dotnet-gitlab-sample` and add a second Gitea webhook, leaving the first unchanged. Set **Target URL** to:

```text
http://gitlab:8929/api/v4/projects/<DOTNET_CI2_PROJECT_ID>/ref/main/trigger/pipeline?token=<DOTNET_CI2_TRIGGER_TOKEN>
```

Replace `<DOTNET_CI2_PROJECT_ID>` with the new project's number and `<DOTNET_CI2_TRIGGER_TOKEN>` with the token from the previous step. Keep the method `POST`, content type `application/json`, and the push branch filter `main`.

A single push to `admin/dotnet-gitlab-sample` now starts pipelines in both `dotnet-ci` and `dotnet-ci2` when both webhooks are active.

### What The Second Pipeline Runs

Push a change to the Gitea application repository and open **CI/CD > Pipelines** in `dotnet-ci2`. The pipeline runs the four catalog jobs in `fetch`, `build`, `test`, and `publish`, plus five security jobs in a new `scan` stage between `test` and `publish`:

| Job | Source | Purpose |
| --- | --- | --- |
| `git-clone` | Gitea template | Clone the source into `application/` |
| `dotnet-build` | Gitea template | Restore and build |
| `dotnet-test` | Gitea template | Run the xUnit tests |
| `semgrep-sast` | GitLab SAST | Static analysis of the source |
| `secret_detection` | GitLab secret detection | Find leaked secrets |
| `dependency-scanning` | GitLab dependency scanning | Known CVEs in dependencies |
| `kics-iac-sast` | GitLab IaC scanning | Scan the Dockerfile and infrastructure |
| `code_quality` | GitLab code quality | CodeClimate-based quality report |
| `dotnet-publish` | Gitea template | Publish the application |

The security jobs are overridden to read the source the clone job downloaded into `application/`, because the orchestration repository is empty and the global `GIT_STRATEGY` is `empty`. Each keeps its `*_DISABLED` opt-out.

`code_quality` requires Docker-in-Docker (the bundled job runs `docker pull` and `docker run`), which this lab's Runner does not provide. `dependency-scanning` needs internet access to fetch its vulnerability database from `gitlab.com`, which an air-gapped local lab cannot reach. Both jobs are marked `allow_failure`, so they do not block the pipeline. SAST, secret detection, and IaC scanning run in the default Docker executor and produce their report artifacts.

![GitLab second pipeline showing the scan stage with three passing analyzers and two allow_failure scans](../../../blog/static/images/posts/gitlab-pipelines-as-code-with-gitea-and-docker-compose/gitlab-security-scan-stage.png)

## Verify The Published Application

Download the artifacts after `dotnet-publish` succeeds:

1. Open the `dotnet-publish` job.
2. Choose **Browse** or **Download** to get the job artifacts as a zip file.
3. Extract the archive into this sample's `evidence/` directory, so that `evidence/publish/Sample.Api.dll` exists.
4. Start the published application and check both endpoints:

```bash
docker compose --profile verify up -d --wait sample-app
curl -s http://localhost:5080/health
curl -s http://localhost:5080/weatherforecast
```

Expected responses:

```text
{"status":"healthy"}
[{"day":1,"temperatureC":-5,"summary":"Freezing","temperatureF":23}, ...]
```

Stop the verification container with:

```bash
docker compose --profile verify rm -sf sample-app
```

Name the service explicitly. A plain `down` removes the GitLab, Gitea, and Runner containers as well, which stops the whole lab instead of only the verification container. Container data lives in volumes, so `down` does not delete the GitLab project, the variables, or the Gitea repositories, but the stack has to be started again afterwards.

`sample-app` mounts `evidence/publish` instead of building the Dockerfile, so the check exercises the artifact that GitLab produced. An already-running container keeps the previously loaded assembly; recreate it after every new download.

## Configuration Files

- [deployments/docker-compose.yaml](deployments/docker-compose.yaml): pinned image tags, ports, services, network, volumes, and health checks.
- [deployments/gitea/app.ini](deployments/gitea/app.ini): tracked Gitea settings used to seed the data volume.
- [deployments/gitlab/gitlab.rb](deployments/gitlab/gitlab.rb): GitLab URL, disabled services, and reduced local resource settings.
- [application/.gitlab-ci.yml](application/.gitlab-ci.yml): application entry point, including the shared pipeline component and passing the source URL and revision inputs.
- [gitlab-templates/](gitlab-templates/): separate Gitea repository with the pipeline component and the reusable clone, build, test, and publish job components.
- [dotnet-ci/README.md](dotnet-ci/README.md): README-only GitLab project contents and instructions for publishing them.

GitLab has no supported `gitlab.rb` switch for creating arbitrary projects, variables, trigger tokens, or Runner registrations. Those remain one-time UI steps described above.

Host ports can be overridden with `GITLAB_HTTP_PORT`, `GITEA_HTTP_PORT`, `GITEA_SSH_PORT`, and `APP_HTTP_PORT`. Pass one inline, for example `GITEA_SSH_PORT=2223 docker compose up -d`, or put it in an untracked `.env` file next to `docker-compose.yaml`, which Compose loads automatically. Internal container ports stay fixed. The Gitea SSH host port defaults to `22222` because `2222` was reserved on the Windows validation machine.

## Repository Ownership

The Gitea `admin/dotnet-gitlab-sample` repository contains the application, tests, Dockerfile, and a small `.gitlab-ci.yml`. The separate `admin/gitlab-templates` repository owns `pipelines/dotnet-ci.yml` and the job components under `templates/`. Both are public on this local instance so GitLab can retrieve remote YAML without authentication and the clone job needs no source-access secret.

The application entry point includes the shared pipeline component and supplies its source inputs:

```yaml
include:
  - remote: 'http://gitea:3000/admin/gitlab-templates/raw/branch/main/pipelines/dotnet-ci.yml'
    inputs:
      source_url: $GIT_CLONE_URL
      source_revision: $GIT_CLONE_REVISION
```

Every file in the catalog is written in GitLab's CI/CD component format: a `spec:` header with `spec:inputs`, then `---`, then the jobs that read those inputs with `$[[ inputs.<name> ]]`. The pipeline component forwards its own inputs into each job component through `include:inputs`, because an input is only visible in the file that declares it. Each job file is self-contained: the .NET jobs declare their own SDK image, NuGet cache, and artifact paths, and the clone job declares its own git image. Nested remote includes use explicit URLs because GitLab resolves them without project-variable context. See [gitlab-templates/README.md](gitlab-templates/README.md) for the component contract, the reason this catalog uses `include:remote` instead of `include:component`, and versioning guidance.

The private GitLab `gitea-dotnet/dotnet-ci` project remains the orchestration project. Its repository contains only documentation; its project settings retain CI variables, trigger credentials, pipeline history, logs, and artifacts. Runner assignment now lives at the group level, not on the project. Do not delete the project, and do not copy pipeline YAML into it.

## Push And Pipeline Flow

1. A developer pushes to Gitea's configured application branch, `main` by default.
2. Gitea sends a JSON push webhook to the GitLab trigger endpoint with the trigger token.
3. GitLab resolves the CI project's `main` ref, loads the application entry point from Gitea, and resolves the shared pipeline component and its job components from `gitlab-templates`. The pipeline has `CI_PIPELINE_SOURCE=trigger`.
4. `git-clone` clones `GIT_CLONE_URL` at `GIT_CLONE_REVISION` into `application/` and records the resolved commit.
5. `dotnet-build` restores and builds the application and test project in the .NET SDK image.
6. `dotnet-test` runs three xUnit tests and retains the TRX result file.
7. `dotnet-publish` publishes the previously built application and retains it with `source-commit.txt`.

The four stages run in order, and each stage downloads the artifacts of the stages before it. That is how the clone job's dotenv report reaches `dotnet-publish` without an explicit `needs` entry, and why every job remains installable on its own.

The webhook URL's `main` is the GitLab orchestration branch. Gitea's application branch is configured independently. `CI_COMMIT_SHA` identifies GitLab's README-only project commit, not the application or external YAML revision. `CI_CONFIG_PATH` identifies the external URL, while `source-commit.txt` records the exact Gitea application revision the clone job resolved.

The application and component URLs follow branches, so GitLab fetches their latest YAML when creating a pipeline. The cloned source resolves `GIT_CLONE_REVISION` at run time, so a concurrent push can advance the branch before the clone job runs; the clone records the commit it actually resolved in `source-commit.txt`. This sample does not provide event-pinned source or exact-commit YAML provenance; that would require additional orchestration to select an immutable configuration per event. Pin the entire include chain for immutable component releases, not only the outer pipeline. The catalog repository has no webhook: component changes affect subsequent application runs but do not trigger one automatically.

Every job uses `GIT_STRATEGY: empty`; Runner does not try to clone the CI repository or the application automatically. Artifacts carry the cloned source and built output between jobs. NuGet packages are cached by Runner.

Runner-created job containers explicitly join the `local-gitlab-ci` Docker network, enabling `gitlab:8929` and `gitea:3000` resolution. Browser links use localhost; internal traffic never uses localhost to reach another container.

The Runner image manages jobs; it is not the application build image. The git image handles source retrieval, and the .NET SDK image handles .NET stages. No Docker-in-Docker or privileged build containers are used. The application Dockerfile is included for independent image builds, but this pipeline publishes application binaries rather than building or pushing an application image.

## Verify The Pipeline Itself

The pipeline is only proven when a real Gitea push produces a successful run. Check:

- The pipeline's source is `trigger`, not a manual run.
- `source-commit.txt` in the publish artifacts names the commit you pushed.
- `TestResults/tests.trx` reports three executed, three passed, zero failed tests.
- The clone job log shows the fetch of the configured `GIT_CLONE_URL`.
- The downloaded `publish/Sample.Api.dll` starts and serves both endpoints.

Tests are `Should_ReturnForecastValues_When_ForecastRequested`, `Should_ReturnHealthy_When_HealthRequested`, and `Should_ReturnNotFound_When_UnknownEndpointRequested`.

A passing test job, an uploaded TRX file, and GitLab's **Tests** tab are separate pieces of evidence. This sample uploads TRX as a regular artifact, not a `reports:junit` report, so the **Tests** tab can show zero.

## Security Boundaries

The GitLab root password is generated inside the container, and the Gitea administrator uses the documented local default until you change it. Both are disposable local-lab credentials; never reuse them, and change the password plus enable two-factor authentication right after the first sign-in.

All published ports bind to `127.0.0.1`; internal HTTP traffic is unencrypted. Containers on the `local-gitlab-ci` network can still reach every service.

GitLab's outbound allowlist contains only the configured Gitea host and port. The external URL and every nested template URL require unauthenticated access; private repositories would need a different configuration delivery design. Anyone able to change application YAML or shared templates can run jobs with this project's CI permissions, so write access to either repository is a CI security boundary.

Runner has access to the host Docker socket and therefore effective control over the Docker host. Only run trusted jobs here. The socket is not mounted into build containers, and jobs run without privileged mode.

The GitLab trigger token authenticates webhook delivery. GitLab's trigger endpoint does not validate Gitea HMAC signatures. Add a signature-validating relay before exposing this integration outside a trusted local network, and restrict access to Gitea webhook settings, which contain the trigger URL.

Pipeline status and logs are visible in GitLab. This minimal flow does not post commit statuses back to Gitea or implement Gitea pull-request checks.

## Stop And Troubleshoot

```bash
docker compose ps
docker compose logs --tail 80 gitlab gitea runner
docker compose stop
```

Stopping preserves repositories, accounts, Runner registration, and artifacts. Start again with `docker compose up -d gitlab gitea runner`.

| Symptom | First checks |
| --- | --- |
| `initial_root_password` file missing | Password already changed or file expired; reset it with the rake command |
| External configuration cannot be loaded | Project **CI/CD configuration file** URL, public Gitea file, GitLab outbound allowlist, remote lint errors |
| Jobs remain pending | Runner online in **Build > Runners**, group assignment, `docker` tag |
| Fetch cannot resolve `gitea` or reach GitLab | Runner registration `--docker-network-mode local-gitlab-ci` |
| Runner exits immediately after start | Registration never ran, or the token was rotated; rerun registration |
| No pipeline after a Gitea push | Webhook **Recent Deliveries**, push event, branch filter, Gitea egress allowlist |
| Trace upload fails with `CI job token signing key is not set` | Initialize the missing key once (below) |
| Test job passes but **Tests** tab is empty | TRX artifact versus parsed JUnit reporting |
| API unhealthy after publishing | Recreated `sample-app`, mounted `evidence/publish`, IPv4 health probe |

If Runner trace upload fails with `CI job token signing key is not set`, initialize the missing keys once without replacing existing ones:

```bash
docker compose exec gitlab gitlab-rails runner \
  "s=ApplicationSetting.current; s.ci_jwt_signing_key ||= OpenSSL::PKey::RSA.new(2048).to_pem; s.ci_job_token_signing_key ||= OpenSSL::PKey::RSA.new(2048).to_pem; s.save!"
```

After editing [deployments/gitlab/gitlab.rb](deployments/gitlab/gitlab.rb), restart GitLab so its container startup reapplies the configuration:

```bash
docker compose restart gitlab
```

The local Puma process uses a 3 GB watchdog budget. The default 1.5 GB limit caused repeated restarts and HTTP 502 responses during validation. A watchdog threshold is not a Docker memory reservation.

The first Docker job can take longer while Runner downloads its version-matched helper image. NuGet restore also needs external access on the first run.

To discard all local state and start over, remove the volumes with `docker compose down -v`. This deletes repositories, accounts, Runner registration, and pipeline history.

References: [GitLab external CI configuration](https://docs.gitlab.com/ci/pipelines/settings/#specify-a-custom-cicd-configuration-file), [outbound request filtering](https://docs.gitlab.com/security/webhooks/), [pipeline triggers](https://docs.gitlab.com/ci/triggers/), [Docker executor](https://docs.gitlab.com/runner/executors/docker/), [group Runner registration](https://docs.gitlab.com/ci/runners/runners_scope/#create-a-group-runner-with-a-runner-authentication-token), and [Gitea webhooks](https://docs.gitea.com/usage/webhooks/).
