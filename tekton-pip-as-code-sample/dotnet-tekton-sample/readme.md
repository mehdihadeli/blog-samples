# Local .NET DevSecOps with Tekton and Argo CD

Gitea hosts application source, the shared Tekton catalog, container packages, and a separate GitOps repository. Tekton checks source, scans and signs an image, and commits a verified digest. Argo CD owns runtime deployment.

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

For an existing cluster, provision the registry mirror through your normal k3d/k3s configuration. Insecure Buildah flags do not configure kubelet pulls. Do not delete/recreate an existing cluster to follow this example. Both nodes and task pods must resolve and reach `host.k3d.internal:3000`, the registry address used by images and pull Secrets.

On Docker Desktop, the [CoreDNS override](deployments/coredns-local-registry.yaml) rewrites the registry hostname to `host.docker.internal` for Pods. Apply it and restart CoreDNS on an existing k3s cluster too. The override relies on k3s's custom Corefile import; other Kubernetes distributions require their equivalent DNS configuration. Node image pulls use the registry mirror's Docker Desktop endpoint instead of Pod DNS.

Wait for the controllers:

```bash
kubectl get pods -n tekton-pipelines
kubectl get pods -n pipelines-as-code
```

## Configure Gitea and PaC

Gitea's registry authentication challenge uses its configured `ROOT_URL`. A default `http://localhost:3000/` advertises `/v2/token` on the task pod's own loopback address and breaks registry authentication. The [Gitea Compose configuration](deployments/gitea-setup/docker-compose.yaml) sets `ROOT_URL` to `http://host.docker.internal:3000/`, reachable from task pods and Docker Desktop node containers. The image registry itself remains `host.k3d.internal:3000`; verify both names resolve in your cluster.

Apply that configuration before configuring PaC or running releases:

```bash
docker compose -f deployments/gitea-setup/docker-compose.yaml up -d
```

This restarts Gitea with the canonical URL changed. `Repository.spec.url` now matches that URL rather than `localhost`. You can still open the local UI at `http://localhost:3000`; existing webhooks need their repository matching configuration updated after the restart.

Create a Gitea token with repository access and a webhook secret, then create the Kubernetes secret:

```bash
kubectl create secret generic gitea-webhook-config \
  --from-literal=provider.token="<YOUR_GITEA_TOKEN>" \
  --from-literal=webhook.secret="<YOUR_WEBHOOK_SECRET>" \
  -n default
kubectl apply -f deployments/repo.yaml
```

Ensure Gitea allows the local destination in `gitea/gitea/conf/app.ini`:

```ini
[webhook]
ALLOWED_HOST_LIST = host.docker.internal, private
```

Restart Gitea after changing `app.ini`. Keep this forward running:

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

- [dotnet-ci](../tekton-catalog/pipelines/dotnet-ci.yaml): formatting/build/tests, Git history secrets, Dockerfile lint, SAST, and source dependency/IaC scanning
- [dotnet-devsecops](../tekton-catalog/pipelines/dotnet-devsecops.yaml): repeat source gates, build a candidate image, scan exact digest, sign image/SBOM, verify with a public key, and commit GitOps
- The original build-only pipeline, retained for lightweight experimentation

Relative task paths resolve inside the remote catalog repository. Hadolint and Buildah resolve directly from a commit-pinned upstream Tekton catalog. Local URLs use catalog `main`; pin reviewed raw commit URLs for protected releases.

## Application PipelineRun

The application `.tekton/push.yaml` must provide a clone URL reachable from a Tekton pod. With Docker Desktop on Windows, use `host.docker.internal`, not `localhost`:

```yaml
pipelinesascode.tekton.dev/pipeline: "http://host.docker.internal:3000/admin/tekton-catalog/raw/branch/main/pipelines/dotnet-devsecops.yaml"
```

This sample passes:

```yaml
- name: repo-url
  value: "http://host.docker.internal:3000/admin/dotnet-tekton-sample.git"
- name: revision
  value: "{{ revision }}"
- name: branch
  value: main
- name: project-directory
  value: src
- name: test-project
  value: tests/dotnet-tekton-sample.Tests.csproj
- name: image-repository
  value: host.k3d.internal:3000/admin/dotnet-tekton-sample
- name: gitops-url
  value: http://host.docker.internal:3000/admin/dotnet-gitops.git
```

The complete [push PipelineRun](.tekton/push.yaml) binds release Secrets and explicitly allows the local HTTP registry. The [PR PipelineRun](.tekton/pull-request.yaml) uses credential-free checks and matches only same-repository PRs targeting `main`. The sample targets `net10.0` and has [real API smoke tests](tests/WeatherForecastTests.cs) using xUnit v2/VSTest.

**Security:** This local setup assumes trusted collaborators. Separate service accounts do not isolate namespace Secrets. Untrusted/fork-provided PipelineRuns need separate credential-free namespaces, protected catalog definitions, and admission policy. Buildah is privileged, but neither Tekton service account receives workload deployment RBAC.

## Prepare Release Credentials

Create Gitea repositories `tekton-catalog`, `dotnet-tekton-sample`, and `dotnet-gitops`. Source examples assume public read access. Seed the [GitOps tree](../dotnet-gitops/README.md) on `dotnet-gitops/main` before first release.

Create separate Gitea tokens for package read/write, runtime package read-only, and GitOps repository write. Use least privilege and dedicated bots where possible. Do not store the local administrator password or any tokens in Git.

Apply service accounts and the runtime namespace:

```bash
kubectl apply -f deployments/bootstrap.yaml
kubectl apply -f deployments/repo.yaml
```

Repository `concurrency_limit: 1` serializes local runs. Promotion also fails if source `main` advanced since checkout; rerun the latest commit rather than promoting stale state.

Enter values directly in your terminal, not chat:

```bash
read -r -p 'Gitea username: ' GITEA_USER
read -r -s -p 'Registry read/write token: ' GITEA_PACKAGE_TOKEN
printf '\n'
kubectl create secret docker-registry gitea-registry -n default \
  --docker-server=host.k3d.internal:3000 \
  --docker-username="$GITEA_USER" --docker-password="$GITEA_PACKAGE_TOKEN"
unset GITEA_PACKAGE_TOKEN

read -r -s -p 'Registry read-only token: ' GITEA_PULL_TOKEN
printf '\n'
kubectl create secret docker-registry gitea-pull -n dotnet-sample \
  --docker-server=host.k3d.internal:3000 \
  --docker-username="$GITEA_USER" --docker-password="$GITEA_PULL_TOKEN"
unset GITEA_PULL_TOKEN

read -r -s -p 'GitOps repository write token: ' GITOPS_TOKEN
printf '\n'
kubectl create secret generic gitops-auth -n default \
  --from-literal=username="$GITEA_USER" --from-literal=token="$GITOPS_TOKEN"
unset GITOPS_TOKEN GITEA_USER
```

With Cosign v3.0.6 installed locally, generate an encrypted keypair outside all repositories. Use a fresh prefix if files already exist; never overwrite an existing signing identity:

```bash
pushd "$HOME"
read -r -s -p 'New Cosign key password: ' COSIGN_PASSWORD
printf '\n'
export COSIGN_PASSWORD
cosign generate-key-pair --output-key-prefix tekton-local-cosign
kubectl create secret generic cosign-signing-key -n default \
  --from-file=cosign.key=tekton-local-cosign.key \
  --from-literal=password="$COSIGN_PASSWORD"
kubectl create secret generic cosign-public-key -n default \
  --from-file=cosign.pub=tekton-local-cosign.pub
unset COSIGN_PASSWORD
popd
```

Keep key material under your normal backup/rotation policy. Manage the trusted public-key Secret separately from application authors. This local sample uses keyed signing without Rekor, not OIDC/keyless signing. Production should use TLS and KMS-backed keys or properly configured Tekton Chains/OIDC and Sigstore services.

## Bootstrap Argo CD

Install Argo CD only in the intended cluster:

```bash
kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply -f -
kubectl apply --server-side -n argocd \
  -f https://raw.githubusercontent.com/argoproj/argo-cd/v3.5.1/manifests/install.yaml
kubectl rollout status deployment/argocd-server -n argocd --timeout=180s
```

The [AppProject](deployments/argocd/project.yaml) permits only the GitOps repository, `dotnet-sample` namespace, and Deployment/Service/NetworkPolicy resources. Namespace and pull Secret creation are separate administrator operations.

For private GitOps, register a separate read-only repository token with Argo CD first:

```bash
read -r -p 'Gitea username: ' GITEA_USER
read -r -s -p 'GitOps read-only token for Argo CD: ' ARGO_GIT_TOKEN
printf '\n'
kubectl create secret generic dotnet-gitops-repository -n argocd \
  --from-literal=type=git \
  --from-literal=url=http://host.docker.internal:3000/admin/dotnet-gitops.git \
  --from-literal=username="$GITEA_USER" --from-literal=password="$ARGO_GIT_TOKEN"
kubectl label secret dotnet-gitops-repository -n argocd argocd.argoproj.io/secret-type=repository
unset ARGO_GIT_TOKEN GITEA_USER
```

Apply project before [Application](deployments/argocd/application.yaml):

```bash
kubectl apply -f deployments/argocd/project.yaml
kubectl apply -f deployments/argocd/application.yaml
```

Argo CD watches GitOps `main` at `apps/dotnet-tekton-sample/overlays/local` with automatic sync, pruning, and self-healing. The initial all-zero digest is deliberately not deployable; health cannot become successful until first promotion replaces it. Alternatively, bootstrap the Application after first promotion.

Runtime uses UID 1654, read-only root filesystem, dropped capabilities, CPU/memory limits, HTTP probes, and writable `/tmp`. NetworkPolicy permits port 8080 from cluster namespaces and denies egress. Adjust desired policy through GitOps when outbound dependencies or narrower ingress rules are needed.

## Run and inspect

Push the application repository after `.tekton/push.yaml` is on `main`:

```bash
kubectl get pipelinerun -n default -w
kubectl get taskrun -n default
```

A successful release has `SUCCEEDED=True`, successful source/image gates, and `verified-image`/`gitops-commit` results. For logs and results:

```bash
kubectl logs -n default <build-dotnet-pod> -c step-build
kubectl get pipelinerun <run-name> -n default -o jsonpath='{.status.results}'
```

Reports remain under `reports/` on the shared per-run PVC: TRX, SARIF, verified image, and finally-generated `ci-evidence.json`. Image scanning and the image CycloneDX SBOM task are temporarily disabled in `dotnet-devsecops`; re-enable `scan-image` before treating this sample as a complete vulnerability gate. Retain/export the PVC before deleting runs. The summary is not signed SLSA provenance. A failed signing or verification step cannot update GitOps.

GitOps publication is not proof of a healthy deployment. Compare Argo CD's sync revision to the exact `gitops-commit` result, and require Synced/Healthy:

```bash
kubectl get application dotnet-tekton-sample -n argocd \
  -o jsonpath='{.status.sync.revision}{"\n"}{.status.sync.status}{"\n"}{.status.health.status}{"\n"}'
kubectl rollout status deployment/dotnet-tekton-sample -n dotnet-sample --timeout=180s
kubectl port-forward -n dotnet-sample service/dotnet-tekton-sample 8085:80
```

In another terminal:

```bash
curl --fail http://localhost:8085/weatherforecast
```

Roll back by reverting the GitOps promotion commit. Do not use direct `kubectl set image` or Tekton deployment steps; Argo CD self-healing would overwrite those changes.

Common failures:

- `cannot find a repository match`: make `Repository.spec.url` match Gitea's payload URL.
- `cannot find referenced task`: verify the catalog Pipeline URL and relative `../tasks/*.yaml` paths.
- `MSB1003`: set `project-directory` to the directory containing the project, such as `src`.
- webhook connection refused: start the PaC port-forward and allow `host.docker.internal` in Gitea.
- registry failure: check node/pod DNS, HTTP mirror, package permissions, and Docker Secret server name.
- zero discovered tests: provide a real VSTest test project; native MTP needs a runner-specific Task.
- signature failure: validate trusted public key; never bypass verification to promote.
- `Source branch advanced`: a newer push superseded this run; rerun latest source.
- GitOps push conflict: another writer changed GitOps; rerun against current state, never force-push.
- Argo CD `ImagePullBackOff`: check promoted digest, runtime package token, and node mirror. The bootstrap all-zero digest cannot run.

CodeQL, Sonar, Snyk, Dependency-Track, coverage publishing, and post-deployment DAST are not automatically included. See the catalog's explicit scope notes before treating portable scanners as equivalent to every reference service.
