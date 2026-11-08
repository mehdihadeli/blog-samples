did# .NET Sample GitOps Repository

Publish this directory as the separate Gitea repository `admin/dotnet-gitops` on branch `main`. It contains desired runtime state, not application source or Tekton definitions.

## Layout

- [Deployment](apps/dotnet-tekton-sample/base/deployment.yaml): hardened runtime, resource bounds, HTTP probes.
- [Service](apps/dotnet-tekton-sample/base/service.yaml): internal HTTP endpoint.
- [NetworkPolicy](apps/dotnet-tekton-sample/base/network-policy.yaml): cluster ingress on 8080 and denied egress.
- [Local overlay](apps/dotnet-tekton-sample/overlays/local/kustomization.yaml): namespace and promoted immutable digest.

The all-zero digest is an intentional bootstrap placeholder, not a real image. First successful Tekton release replaces it.

## Seed

Create an empty `dotnet-gitops` repository in Gitea, then run from this directory after reviewing files:

```bash
git init -b main
git add apps README.md
git commit -m "Initialize .NET GitOps desired state"
git remote add origin http://localhost:3000/admin/dotnet-gitops.git
git push -u origin main
```

Do not embed tokens in remote URLs. Use normal credential management. Protect `main`; allow the local promotion bot, but require reviewed promotion PRs for production environments.

## Promotion

Tekton's GitOps Task changes exactly the image entry named `dotnet-tekton-sample`. It sets `newName` and `digest`, removes `newTag`, stages only the configured Kustomization, and makes a normal fast-forward push. Stale source revisions and GitOps push conflicts fail rather than overwriting newer state.

PaC concurrency serializes this sample's runs. Other GitOps writers or release controllers require coordinated promotion policy. Argo CD watches the overlay with automatic sync and self-healing; see the application setup guide for bootstrap and registry Secrets.

Successful Tekton publication is not proof of runtime deployment. Confirm Argo CD is Synced and Healthy at the exact reported `gitops-commit` before declaring a release deployed.

## Validate and Roll Back

```bash
kubectl kustomize apps/dotnet-tekton-sample/overlays/local
git revert <promotion-commit>
git push origin main
```

Argo CD reconciles the reverted digest. Do not store passwords, tokens, signing keys, or Argo CD administrator credentials here. Runtime pull Secrets are provisioned separately by an administrator.
