# Local .NET CI with Tekton and Gitea

Gitea hosts the application source and shared Tekton catalog. Pipelines-as-Code receives Gitea events and runs a shared Tekton Pipeline that clones the event revision and builds the .NET project.

## Prerequisites

- Docker Desktop with WSL2 integration
- k3d and kubectl
- Optional: `tkn`
- Gitea available at `http://localhost:3000`

## Install the cluster components

Run commands from the application repository root in Git Bash. For a new cluster, the provided k3d configuration adds an HTTP registry mirror and does not replace an existing cluster:

```bash
k3d cluster create --config deployments/k3d-devsecops.yaml
kubectl config use-context k3d-tekton-devsecops
kubectl apply -f deployments/coredns-local-registry.yaml
kubectl rollout restart deployment/coredns -n kube-system
kubectl apply -f https://infra.tekton.dev/tekton-releases/pipeline/latest/release.yaml
kubectl apply -f https://github.com/tektoncd/pipelines-as-code/releases/download/v0.50.2/release.k8s.yaml
kubectl apply -f https://github.com/tektoncd/dashboard/releases/download/v0.74.0/release.yaml
```

For an existing cluster, use your normal k3d/k3s configuration. Do not delete/recreate an existing cluster to follow this example. Task pods must resolve and reach `host.docker.internal:3000` for the Gitea clone URL.

On Docker Desktop, the [CoreDNS override](deployments/coredns-local-registry.yaml) rewrites the registry hostname to `host.docker.internal` for Pods. Apply it and restart CoreDNS on an existing k3s cluster too. The override relies on k3s's custom Corefile import; other Kubernetes distributions require their equivalent DNS configuration. Node image pulls use the registry mirror's Docker Desktop endpoint instead of Pod DNS.

Wait for the controllers:

```bash
kubectl get pods -n tekton-pipelines
kubectl get pods -n pipelines-as-code
```

## Configure Gitea and PaC

Gitea's registry authentication challenge uses its configured `ROOT_URL`. A default `http://localhost:3000/` advertises `/v2/token` on the task pod's own loopback address and breaks registry authentication. The [Gitea Compose configuration](deployments/gitea-setup/docker-compose.yaml) sets `ROOT_URL` to `http://host.docker.internal:3000/`, reachable from task pods and Docker Desktop node containers. The image registry itself remains `host.k3d.internal:3000`; verify both names resolve in your cluster.

The tracked [app.ini](deployments/gitea-setup/app.ini) contains the Gitea settings. Compose runs a configuration service that merges them into the data volume before Gitea starts, preserving existing security keys and saving a backup when settings change. Apply that configuration before configuring PaC or running builds:

```bash
docker compose -f deployments/gitea-setup/docker-compose.yaml up -d
```

Gitea initializes automatically. Open http://localhost:3000 and sign in with username `admin` and password `admin`. The startup script creates this administrator only when absent and never resets an existing account. Compose contains these local bootstrap credentials; the INI enables unattended installation and permits the sample password. Published Gitea ports bind only to loopback. These weak credentials are for trusted local development only.

This restarts Gitea with the canonical URL changed. `Repository.spec.url` now matches that URL rather than `localhost`. You can still open the local UI at `http://localhost:3000`; existing webhooks need their repository matching configuration updated after the restart.

Create a Gitea token with repository access and a webhook secret, then create the Kubernetes secret:

```bash
kubectl create secret generic gitea-webhook-config \
  --from-literal=provider.token="<YOUR_GITEA_TOKEN>" \
  --from-literal=webhook.secret="<YOUR_WEBHOOK_SECRET>" \
  -n default
kubectl apply -f deployments/bootstrap.yaml
kubectl apply -f deployments/repo.yaml
```

The tracked INI already allows the local webhook destination:

```ini
[security]
ALLOWED_HOST_LIST = host.docker.internal:8080, private:8080
```

After changing the tracked INI, apply it and restart Gitea:

```bash
docker compose -f deployments/gitea-setup/docker-compose.yaml run --rm configure
docker compose -f deployments/gitea-setup/docker-compose.yaml restart server
```

Keep this forward running:

```bash
kubectl port-forward --address 0.0.0.0 svc/pipelines-as-code-controller 8080:8080 -n pipelines-as-code
```

Keep a second terminal open for the Tekton Dashboard:

```bash
kubectl port-forward --address 127.0.0.1 svc/tekton-dashboard 9097:9097 -n tekton-pipelines
```

Use `http://localhost:9097/#/pipelineruns` to inspect PipelineRuns and TaskRuns.

Configure a Gitea push webhook for `dotnet-tekton-sample`:

- URL: `http://host.docker.internal:8080`
- Content type: JSON
- Secret: the same webhook secret
- Events: Push and Pull Request

The `spec.url` in `deployments/repo.yaml` must match the repository URL in the webhook payload exactly. The `git_provider.url` is the address PaC uses to call Gitea from the cluster.

## Shared catalog

Publish the [shared catalog](../tekton-catalog/README.md) as the separate Gitea repository `tekton-catalog`. It contains:

- [dotnet-ci](../tekton-catalog/pipelines/dotnet-ci.yaml): clone the source repository and build the .NET project
- The pipeline clones the repository with the pinned upstream Tekton Catalog `git-clone` task and runs the local `dotnet-build` task.

The Pipeline resolves the local `dotnet-build` Task relative to the catalog repository and pins the upstream `git-clone` Task to an immutable catalog commit. The application references the catalog's `main` branch for this local example; protected environments should use an immutable catalog URL.

## Application PipelineRun

The application `.tekton/ci.yaml` provides a clone URL reachable from a Tekton pod. With Docker Desktop on Windows, use `host.docker.internal`, not `localhost`:

```yaml
pipelinesascode.tekton.dev/pipeline: http://host.docker.internal:3000/admin/tekton-catalog/raw/branch/main/pipelines/dotnet-ci.yaml
```

The application passes only the parameters required by the shared Pipeline:

```yaml
- name: repo-url
  value: "http://host.docker.internal:3000/admin/dotnet-tekton-sample.git"
- name: revision
  value: "{{ revision }}"
- name: project-directory
  value: src
```

The complete [PipelineRun](.tekton/ci.yaml) runs for pushes and same-repository pull requests targeting `main`. Its CEL filter excludes fork pull requests, and its `revision` value makes the build use the event commit. The sample targets `net10.0`; this pipeline performs a build and does not run tests or deploy the application.

**Security:** This local setup assumes trusted collaborators. Protect the catalog repository and PipelineRun template. Untrusted fork pull requests are excluded by the CEL filter; use a separate credential-free policy if fork validation is required.

## Run and inspect

Commit the application repository after `.tekton/ci.yaml` is on `main`, then push it to Gitea:

```bash
git add .tekton/ci.yaml
git commit -m "Run shared Tekton build pipeline"
git push origin main
```

PaC receives the Gitea webhook and creates a PipelineRun. Watch the run and its TaskRuns:

```bash
kubectl get pipelinerun -n default -w
kubectl get taskrun -n default
```

A successful run has `SUCCEEDED=True`, a successful `fetch-repository` TaskRun, and a successful `build-dotnet` TaskRun. Inspect the resolved PipelineRun and build logs:

```bash
kubectl get pipelinerun <run-name> -n default -o yaml
kubectl logs -n default <build-dotnet-pod> -c step-build
```

Open `http://localhost:9097/#/pipelineruns` to inspect the same run in Tekton Dashboard. The pipeline exposes the checked-out commit as the `source-commit` result.

Common failures:

- `cannot find a repository match`: make `Repository.spec.url` match Gitea's payload URL exactly.
- `cannot find referenced task`: verify the catalog Pipeline URL and its relative `../tasks/dotnet-build.yaml` path.
- `MSB1003`: set `project-directory` to the directory containing the project, such as `src`.
- webhook connection refused: start the PaC port-forward and allow `host.docker.internal` in Gitea.
- pending PVC: check dynamic provisioning and the cluster's default StorageClass.
