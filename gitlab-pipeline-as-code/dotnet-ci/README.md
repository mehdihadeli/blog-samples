# Dotnet CI Orchestration Project

Publish this directory's contents to the GitLab project `root/dotnet-ci`, not to Gitea. This is the README-only repository for GitLab pipeline orchestration; it does not contain application source or pipeline YAML.

## Repository Responsibilities

- GitLab project: <http://localhost:8929/root/dotnet-ci>.
- Application and pipeline entry point: <http://localhost:3000/admin/dotnet-gitlab-sample>.
- Shared pipeline and job templates: <http://localhost:3000/admin/gitlab-templates>.
- Pipeline history and job logs: <http://localhost:8929/root/dotnet-ci/-/pipelines>.

Keep the GitLab project even though its repository contains only this README. The project owns CI variables, trigger credentials, Runner assignment, pipeline history, logs, and artifacts. Those settings are not stored in this Git repository.

## Create The Project Manually

1. Sign in to <http://localhost:8929> as `root`.
2. Choose **New project > Create blank project**.
3. Name it `dotnet-ci`, keep the namespace `root`, select **Private**, and enable **Initialize repository with a README**.
4. Create the project, then continue with the remaining GitLab settings described in the parent [README.md](../README.md).

Creating the project does not create a Gitea webhook, CI variables, a trigger token, or a Runner registration. Those are separate configuration steps in the project's **Settings > CI/CD** pages.

## Push This README To GitLab

The project starts with a generic README. Replace it with this file from a scratch checkout:

```bash
git clone --branch main http://localhost:8929/root/dotnet-ci.git ../dotnet-ci-checkout
cp README.md ../dotnet-ci-checkout/README.md
git -C ../dotnet-ci-checkout add README.md
git -C ../dotnet-ci-checkout commit -m "Document GitLab orchestration project"
git -C ../dotnet-ci-checkout push origin main
```

Use an unused checkout directory, or reuse an existing clone after pulling its latest `main`. Authenticate as the GitLab `root` account when prompted. Do not put credentials in the remote URL or commit them.

Clone the existing project rather than initializing an unrelated repository or force-pushing: the `main` branch already has history. Push the contents of this directory so `README.md` is at the repository root, not inside another `dotnet-ci` subdirectory.

## Pipeline Configuration

In the project, open **Settings > CI/CD > General pipelines** and set **CI/CD configuration file** to:

```text
http://gitea:3000/admin/dotnet-gitlab-sample/raw/branch/main/.gitlab-ci.yml
```

This is a Docker-network URL fetched by the GitLab server. Browser access uses <http://localhost:3000/admin/dotnet-gitlab-sample/src/branch/main/.gitlab-ci.yml>. Do not replace the internal URL with `localhost`, and do not copy pipeline YAML into this GitLab repository.

The application entry point includes `pipelines/dotnet-ci.yml` from Gitea's separate `gitlab-templates` repository and passes its inputs. That pipeline component includes a clone component and three self-contained .NET job components, each declared with `spec:inputs`. Both repositories must be readable by the GitLab server without authentication.

Push application changes to the application repository and reusable component changes to `gitlab-templates`. Only the application repository has a push webhook, which starts GitLab's clone, build, test, and publish jobs. Catalog changes are picked up by subsequent application pipelines. Pushing this README to GitLab does not start the sample pipeline because its workflow accepts trigger events only.

The clone component records the resolved commit SHA from the configured `GIT_CLONE_REVISION`, so downstream jobs can trace the exact source. The external entry point and nested component URLs follow branches, so concurrent pushes can select newer YAML than the application commit being built. Versioning the components requires pinning every level of the include chain.

These URLs assume the sample's default ports, namespace, repository name, and branch. Adjust them if the deployment configuration differs. Use this setup only as a trusted local development lab.
