# Gitea Setup and Webhook Integration for Tekton

This guide covers running a local Gitea server with an automatically created administrator, pushing the separate `tekton-catalog` and `dotnet-tekton-sample` repositories, and configuring a webhook for Tekton Pipelines-as-Code (PaC).

---

## 📋 Prerequisites
*   Docker and Docker Compose installed.
*   Your local repositories (`tekton-catalog` and `dotnet-tekton-sample`) initialized with Git.
*   PaC port-forward running in a separate terminal:
    ```bash
    kubectl port-forward --address 0.0.0.0 svc/pipelines-as-code-controller 8080:8080 -n pipelines-as-code
    ```

---

## Step 1: Run Gitea via Docker Compose

Use the provided [docker-compose.yaml](docker-compose.yaml) and [app.ini](app.ini). From the application repository root:

```bash
docker compose -f deployments/gitea-setup/docker-compose.yaml up -d
```

The `configure` service merges `app.ini` into `/data/gitea/conf/app.ini` in the named `gitea_gitea-data` volume before Gitea starts. Existing keys not listed in the tracked file, including generated security keys and installation state, remain intact. A changed existing configuration is backed up as `app.ini.bak` inside that volume; this backup contains secrets too. Do not commit the runtime file or its backup.

The tracked settings select SQLite, `main` as the default branch, the canonical URL `http://host.docker.internal:3000/`, SSH on host port `222`, and webhook access to `host.docker.internal`. Public registration is disabled. [bootstrap-admin.sh](bootstrap-admin.sh) initializes the database and creates the administrator from the Compose environment before starting the HTTP server. The installer is disabled with `INSTALL_LOCK`.

**Local development only:** the default username and password are both `admin`. The INI deliberately permits this weak password, and published HTTP/SSH ports bind only to `127.0.0.1`. Containers on the Docker network can still reach Gitea. Do not expose this setup to untrusted networks. For other environments, replace the credentials, restore a strong password policy, and use TLS.

After editing the tracked INI, rerun the configuration service and restart Gitea:

```bash
docker compose -f deployments/gitea-setup/docker-compose.yaml run --rm configure
docker compose -f deployments/gitea-setup/docker-compose.yaml restart server
```

Existing bind-mounted installations need their data migrated to this named volume first. Changing Compose volume types does not move data automatically.

---

## Step 2: Sign In

Open http://localhost:3000 and sign in with username `admin` and password `admin`. No installation form is required. Bootstrap credentials are declared as `GITEA_ADMIN_USERNAME`, `GITEA_ADMIN_PASSWORD`, and `GITEA_ADMIN_EMAIL` in Compose. These are inputs to the startup script, not built-in Gitea INI options.

On subsequent starts, an existing account with that username is left unchanged. Bootstrap never resets an existing password or promotes an existing non-admin user; changing Compose credentials does not change an existing account.

---

## 📂 Step 3: Create Repositories and Push Local Code

1.  **Create an empty repository in Gitea:**
    *   Click **+** (top right) → **New Repository**.
    *   Name it `tekton-catalog`. **Do not** check "Initialize Repository".
    *   Repeat this step to create `dotnet-tekton-sample`.

2.  **Push from your local folders:**
    Open your terminal, navigate to your local `tekton-catalog` folder, and run:
    ```bash
    # Replace <admin> with the username you just created (e.g., admin)
    git remote add origin http://localhost:3000/<admin>/tekton-catalog.git
    git push -u origin main
    ```
    *Repeat these exact steps for your `dotnet-tekton-sample` folder.* The application repository must include [.tekton/ci.yaml](../../.tekton/ci.yaml) on its `main` branch before webhook testing. Commit the intended files in each repository before pushing; a push does not publish uncommitted changes.

    Publish the catalog first. It must contain [pipelines/dotnet-ci.yaml](../../../tekton-catalog/pipelines/dotnet-ci.yaml) and [tasks/dotnet-build.yaml](../../../tekton-catalog/tasks/dotnet-build.yaml). PaC loads the Pipeline from the catalog URL in the application PipelineRun and resolves the local build Task relative to that catalog repository. Do not copy catalog YAML into the application repository.

---

## 🔑 Step 4: Generate Gitea Credentials & Kubernetes Secrets

1.  **Generate an Access Token:**
    *   In Gitea, click your avatar → **Settings** → **Applications** → **Generate New Token**.
    *   Give it `repo` permissions, generate it, and copy the token.
2.  **Choose a Webhook Secret:**
    *   Create a random string (e.g., `my-super-secret-webhook`).
3.  **Create the Kubernetes Secret:**
    ```bash
    kubectl create secret generic gitea-webhook-config \
      --from-literal=provider.token="<YOUR_GITEA_TOKEN>" \
      --from-literal=webhook.secret="my-super-secret-webhook" \
      -n default
    ```

---

## Step 5: Register the Application With PaC

The webhook egress policy is already set in [app.ini](app.ini); no container-side editing is needed. After installing Tekton and PaC as described in the [application README](../../readme.md), apply the service account and Repository:

```bash
kubectl apply -f deployments/bootstrap.yaml
kubectl apply -f deployments/repo.yaml
```

The examples assume the Gitea owner is `admin`. For another owner, update both `deployments/repo.yaml` and `.tekton/ci.yaml`, including the catalog URL and application clone URL. The two example repositories must be readable by the shared catalog resolver and clone Task; this sample uses public repositories on a trusted local server.

---

## 🔗 Step 6: Configure the Webhook

1.  In Gitea, go to your **`dotnet-tekton-sample`** repository.
2.  Click **Settings** → **Webhooks** → **Add Webhook** (Gitea).
3.  Fill in the fields exactly as follows:
    *   **Target URL:** `http://host.docker.internal:8080`
    *   **HTTP Method:** `POST`
    *   **POST Content Type:** `application/json`
    *   **Secret:** `my-super-secret-webhook`
    *   **Trigger On:** `Push Events` and `Pull Request Events`
4.  Click **Add Webhook**.
5.  **Test it:** Click on the webhook you just created, scroll down, and click **Test Delivery**. You should get a green `202 Accepted` response.

---

## ✅ Step 7: Final Check (The Repository CR)

Make sure your Kubernetes `Repository` CR (`repo.yaml`) matches the exact URL Gitea uses for the repository. 

```bash
kubectl get repo -n default -o yaml
```
Ensure the `spec.url` is set to:
`http://host.docker.internal:3000/<admin>/dotnet-tekton-sample`

*(PaC uses `http://host.docker.internal:3000` to talk to the Gitea API, but the `spec.url` must match the exact string Gitea sends in the webhook payload).*

Once this is done, pushing code to the `dotnet-tekton-sample` repository will automatically trigger Tekton!

## Verify a Real Push

After committing an intended application change, push `main`, then inspect the resulting run:

```bash
git push origin main
kubectl get pipelineruns -n default -w
kubectl get taskruns -n default
kubectl get pipelinerun <run-name> -n default -o yaml
```

Verify `Succeeded=True`, successful `fetch-repository` and `build-dotnet` TaskRuns, and a `source-commit` result matching `git rev-parse HEAD`. A successful webhook delivery alone does not prove that the build succeeded. The current pipeline builds the .NET application; it does not run its unit tests or deploy it.