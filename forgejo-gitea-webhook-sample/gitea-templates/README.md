# Reusable Gitea Actions Templates

This directory is a separate Gitea repository, `admin/admin-gitea-templates`,
on the **CI instance** (`gitea-app`, port 3110). It hosts the reusable
`workflow_call` pipeline plus the composite `dotnet-build-test` and
`dotnet-publish` actions, which is the template content this instance shares
with the application repository `admin/dotnet-ci`.

## Why It Lives On The CI Instance

Gitea resolves **reusable workflows** server-side and requires them to live on
the same instance as the caller. `workflow_call` therefore cannot cross
instances, so the reusable workflow sits beside `dotnet-ci`. The composite
actions are referenced with a full URL, which the runner fetches at run time:

```yaml
- uses: http://gitea-app:3000/admin/admin-gitea-templates/actions/dotnet-build-test@main
```

That URL is the CI instance, so the actions also stay close to the runner. The
source instance (`forgejo`, port 3010) holds nothing but application source.

## Contents

- [actions/dotnet-build-test/action.yml](actions/dotnet-build-test/action.yml)
  sets up the .NET SDK, restores with a NuGet cache, then builds, tests and
  publishes in one step.
- [actions/dotnet-publish/action.yml](actions/dotnet-publish/action.yml)
  publishes a prebuilt project and records the source revision (standalone).
- [.gitea/workflows/dotnet-ci.yaml](.gitea/workflows/dotnet-ci.yaml)
  is the reusable `workflow_call` pipeline that `dotnet-ci` calls.

## Image Notes

The `dotnet` runner label maps to `mcr.microsoft.com/dotnet/sdk:10.0-alpine`.
That image ships git and the .NET SDK but no ICU, so the workflow sets
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: "1"`.
