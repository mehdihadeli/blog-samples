# Shared Tekton DevSecOps Catalog

Reusable Tekton v1 resources for .NET applications hosted in Gitea. This design adapts quality, SAST, SCA, SBOM, and signing stages from the GitHub Actions reference. Deployment uses a GitOps commit and Argo CD reconciliation, not direct cluster updates.

## Pipelines

| Pipeline | Purpose | Credentials |
| --- | --- | --- |
| [dotnet-build](pipelines/dotnet-build.yaml) | Existing lightweight build-only example | None for public source |
| [dotnet-ci](pipelines/dotnet-ci.yaml) | Build, formatting, analyzers, tests, secrets, Dockerfile lint, SAST, source SCA | None for public source |
| [dotnet-devsecops](pipelines/dotnet-devsecops.yaml) | Repeat CI gates, build candidate, scan digest, generate SBOM, sign, verify, commit GitOps | Registry, signing key, public key, GitOps token |

Release repeats checks at the exact source revision. A successful PR check is not proof that a later branch revision passed. Image build waits for every source gate; GitOps publication consumes the verified image result, not a mutable tag.

## Tasks

| Task | Contract |
| --- | --- |
| [git-clone](tasks/git-clone.yaml) | Fetch complete history, check out requested revision, return exact commit |
| [dotnet-build](tasks/dotnet-build.yaml) | Configurable SDK/directory; Release build with warnings as errors |
| [dotnet-verify](tasks/dotnet-verify.yaml) | Verify formatting, execute required VSTest test project, fail on missing/empty TRX or nonpassing executed tests |
| [gitleaks](tasks/gitleaks.yaml) | Scan Git history with v8 CLI and redacted SARIF |
| [semgrep](tasks/semgrep.yaml) | Security-audit rules; findings and scanner errors fail |
| [trivy-source](tasks/trivy-source.yaml) | Restored NuGet assets, secrets, Dockerfiles, supported IaC; HIGH/CRITICAL gate |
| [trivy-image](tasks/trivy-image.yaml) | Scan exact digest and generate CycloneDX SBOM; HIGH/CRITICAL gate |
| [cosign-sign](tasks/cosign-sign.yaml) | Sign image and attest SBOM using encrypted local key |
| [cosign-verify](tasks/cosign-verify.yaml) | Verify image and SBOM using separately mounted trusted public key |
| [gitops-update](tasks/gitops-update.yaml) | Change exactly one Kustomize image to verified digest; reject stale source and push conflicts |
| [write-ci-evidence](tasks/write-ci-evidence.yaml) | Finally task: run status, verified image when available, report hashes |

Images are versioned; pin reviewed image digests for production. Semgrep rules resolve online; vendor a reviewed ruleset when reproducible policy is required. The test Task explicitly supports VSTest syntax, not native MTP.

## Upstream Reuse

Pipelines resolve [Hadolint 0.1](https://github.com/tektoncd/catalog/blob/112374ce1262b133a77300bd8133445722605e04/task/hadolint/0.1/hadolint.yaml) directly. Release also resolves [Buildah 0.9](https://github.com/tektoncd/catalog/blob/112374ce1262b133a77300bd8133445722605e04/task/buildah/0.9/buildah.yaml). URLs pin upstream commit `112374ce1262b133a77300bd8133445722605e04`; Buildah's original parameters/results, including `IMAGE_DIGEST`, remain unchanged.

Upstream Gitleaks 0.1 is deprecated and uses v7, so this catalog supplies a v8 wrapper. No deprecated PipelineResources or Conditions are used.

## Application Contract

Modern pipelines require `repo-url`, `revision`, and `test-project`. Defaults cover `branch`, `project-directory`, `sdk-image`, and `dockerfile`. Release additionally requires `image-repository` and `gitops-url`; GitOps branch, manifest path, and image name are configurable. Results expose `verified-image`, `gitops-commit`, and `test-count`.

| Workspace | Contents |
| --- | --- |
| `shared-workspace` | Per-run PVC; `source/`, `reports/`, `gitops/` subdirectories |
| `registry-auth` | Docker `config.json`; only registry-related release Tasks |
| `signing-key` | `cosign.key`; only signing Task |
| `public-key` | Trusted `cosign.pub`; verification never receives private key |
| `gitops-auth` | `username`, `token`; only GitOps Task |

Signing password comes from the `password` key of `signing-password-secret`, defaulting to `cosign-signing-key`. Keep the public key administrator-managed and separate from source and GitOps.

## Evidence and Failure Policy

Reports stay on the per-run PVC under `reports/`: TRX, Gitleaks/Semgrep/Trivy SARIF, image CycloneDX SBOM, verified image, and `ci-evidence.json`. Finally does not depend on missing failed-task results. Retain/export PVCs before deleting runs; there is no automatic object-store or GitHub artifact upload.

The summary is not signed SLSA provenance. Image and SBOM are signed. No coverage threshold is claimed.

Upstream Buildah pushes a candidate before image scanning to return the registry digest. A failed scan can leave an unsigned `ci-<commit>` candidate, but cannot update GitOps. Never deploy candidate tags; restrict registry access and configure cleanup.

## Trust Boundaries

- Protect release PipelineRuns, catalog definitions, signing Secrets, and GitOps branch rules.
- Local PRs are same-repository contributions from trusted collaborators. Different service accounts do not prevent an edited PipelineRun from mounting namespace Secrets. Untrusted contributors need separate credential-free namespaces and admission policy; fork PRs are excluded here.
- Buildah requires a privileged step. Isolate build nodes/namespaces; neither Tekton service account receives workload deployment RBAC.
- GitOps publication uses no Argo CD API token or direct deployment commands. Argo CD owns reconciliation, pruning, and drift correction.
- HTTP Gitea needs explicit `registry-tls-verify=false` and `registry-insecure=true`; production defaults require TLS.
- Local keyed signing disables transparency-log upload and skips Rekor verification. It is not keyless signing. Production should use KMS-backed keys or properly configured Tekton Chains/OIDC and Sigstore services.
- PaC `concurrency_limit: 1` serializes local runs. GitOps also checks source freshness and never force-pushes or overwrites push conflicts.

## Publish and Resolve

Publish this directory as the Gitea `tekton-catalog` repository before application events reference it. PaC resolves relative Tasks within the remote Pipeline's repository.

Local URLs reference catalog `main` for iteration. Protected releases should use a reviewed Gitea raw commit URL, `/raw/commit/<catalog-sha>/pipelines/dotnet-devsecops.yaml`, so relative tasks resolve at the same commit.

See [application setup](../dotnet-tekton-sample/readme.md). The separate GitOps repository contains only runtime desired state.

## Reference Features Not Ported

CodeQL integration/licensing, Sonar, Snyk, Grype as a second engine, Dependency-Track, coverage publishing, and post-deployment DAST require separate service/tool policy and are not included. Portable scanners do not silently replace those integrations. Argo CD health and HTTP smoke checks are documented deployment validation, not automatic release Tasks.
