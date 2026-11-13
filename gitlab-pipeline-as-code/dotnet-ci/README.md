# Dotnet CI Orchestration Project

Publish this directory's contents to the GitLab project `root/dotnet-ci`, not to Gitea. This is the README-only repository for GitLab pipeline orchestration; it does not contain application source or pipeline YAML.

## Repository Responsibilities

- GitLab project: <http://localhost:8929/root/dotnet-ci>.
- Application and pipeline entry point: <http://localhost:3000/admin/dotnet-gitlab-sample>.
- Shared pipeline and job templates: <http://localhost:3000/admin/gitlab-templates>.
- Pipeline history and job logs: <http://localhost:8929/root/dotnet-ci/-/pipelines>.

Keep the GitLab project even though its repository contains only this README. The project owns CI variables, trigger credentials, Runner assignment, pipeline history, logs, and artifacts. Those settings are not stored in this Git repository.

## Push This README To GitLab

The sample's `scripts/up.sh` creates the GitLab project and configures CI automatically. It initializes a generic README but does not publish this documentation file. After startup, run the following from the local `dotnet-ci` directory to publish it:

```bash
git clone --branch main http://localhost:8929/root/dotnet-ci.git ../dotnet-ci-checkout
cp README.md ../dotnet-ci-checkout/README.md
git -C ../dotnet-ci-checkout add README.md
git -C ../dotnet-ci-checkout commit -m "Document GitLab orchestration project"
git -C ../dotnet-ci-checkout push origin main
```

Use an unused checkout directory, or reuse an existing clone after pulling its latest `main`. Authenticate as an authorized GitLab user when prompted; if a personal access token is required, use one with `write_repository` permission. Do not put credentials in the remote URL or commit them.

Clone the existing project rather than initializing an unrelated repository or force-pushing: the bootstrap-created `main` branch already has history. Push the contents of this directory so `README.md` is at the repository root, not inside another `dotnet-ci` subdirectory.

## Pipeline Configuration

Bootstrap sets **Settings > CI/CD > General pipelines > CI/CD configuration file** to:

```text
http://gitea:3000/admin/dotnet-gitlab-sample/raw/branch/main/.gitlab-ci.yml
```

This is a Docker-network URL fetched by the GitLab server. Browser access uses <http://localhost:3000/admin/dotnet-gitlab-sample/src/branch/main/.gitlab-ci.yml>. Do not replace the internal URL with `localhost`, and do not copy pipeline YAML into this GitLab repository.

The application entry point includes `pipelines/dotnet-ci.yml` from Gitea's separate `gitlab-templates` repository. That pipeline includes reusable source-fetch and .NET job templates. Both repositories must be readable by the GitLab server without authentication.

Push application changes to the application repository and reusable job changes to `gitlab-templates`. Only the application repository has a push webhook, which starts GitLab's fetch, build, test, and publish jobs. Catalog changes are picked up by subsequent application pipelines. Pushing this README to GitLab does not start the sample pipeline because its workflow accepts trigger events only.

The application source is pinned to the Gitea webhook's exact commit SHA. The external entry point and nested template URLs follow branches, so concurrent pushes can select newer YAML than the application commit being built. Versioning the templates requires pinning every level of the include chain.

These URLs assume the sample's default ports, namespace, repository name, and branch. Adjust them if the deployment configuration differs. Use this setup only as a trusted local development lab.
