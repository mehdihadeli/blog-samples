# .NET 10 SBOM + Dependency-Track sample

This sample shows an end-to-end flow:

1. Generate an SBOM from a .NET 10 project with CycloneDX.
2. Run Dependency-Track locally with Docker Compose.
3. Upload the SBOM to Dependency-Track for vulnerability analysis.
4. Automate SBOM generation and upload in GitHub Actions.

## Project structure

- `SbomDependencyTrackDemo.AppHost`: Aspire AppHost used to run the sample locally.
- `src/SbomDependencyTrackDemo.Api`: sample .NET 10 API.
- `mise.toml`: local task runner and tool setup.
- `dependency-track/docker-compose.yml`: local Dependency-Track + PostgreSQL stack.
- `scripts/generate-sbom.sh`: generate CycloneDX SBOM (`JSON`).
- `scripts/upload-to-dependency-track.sh`: upload SBOM to Dependency-Track API.
- `.github/workflows/sbom-dependency-track.yml`: CI workflow.

## Prerequisites

- [mise](https://mise.jdx.dev/)
- [Aspire CLI](https://aspire.dev/get-started/install-cli/)
- Docker and Docker Compose
- curl

## 1) Run the sample app

The default local workflow for this sample uses `mise` plus the Aspire AppHost.

```bash
# 1. Install tools from mise.toml
mise install

# 2. Restore packages and local tools
mise run prepare

# 3. Start the app through Aspire
mise run run
```

If you prefer to run it without `mise`, the equivalent commands are:

```bash
dotnet restore SbomDependencyTrackDemo.slnx
dotnet tool restore
dotnet build SbomDependencyTrackDemo.slnx
aspire start
```

## 2) Generate SBOM locally

```bash
mise run sbom:generate
```

Equivalent direct command:

```bash
./scripts/generate-sbom.sh
```

Generated file:

- `artifacts/sbom/sbom.cyclonedx.json`

## 3) Start Dependency-Track locally

```bash
cp dependency-track/.env.example dependency-track/.env
mise run dependency-track:up
```

Equivalent direct command:

```bash
docker compose --env-file dependency-track/.env -f dependency-track/docker-compose.yml up -d
```

Access:

- UI: `http://localhost:8080`
- API: `http://localhost:8081`

Default credentials for first login are often `admin` / `admin`.
Change them immediately.

## 4) Create API key and project in Dependency-Track

In Dependency-Track UI:

1. Create a project (for example: `sbom-dependency-track-demo`, version `0.1.0`).
2. Create a team with `BOM_UPLOAD` permission.
3. Create an API key for that team.
4. Copy the project UUID from project details.

## 5) Upload SBOM from local machine

```bash
export DT_API_KEY="replace-with-api-key"
export DT_PROJECT_UUID="replace-with-project-uuid"

mise run sbom:upload
```

Equivalent direct command:

```bash
./scripts/upload-to-dependency-track.sh
```

## Optional: force a vulnerable package for alert testing

This sample contains a conditional package reference that can be turned on for test-only scenarios:

```bash
dotnet build -p:IncludeVulnerablePackage=true
mise run sbom:generate
mise run sbom:upload
```

Then review findings in Dependency-Track.

## 6) GitHub Actions integration

Use `.github/workflows/sbom-dependency-track.yml` in your repo root.

Required repository secrets:

- `DT_BASE_URL` (for example: `https://dependency-track.example.com`)
- `DT_API_KEY`
- `DT_PROJECT_UUID`

The workflow still generates and stores the SBOM artifact when these secrets are not configured. Upload step is skipped in that case.
