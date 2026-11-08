# 🦊 Gitea Setup & Webhook Integration for Tekton

This guide covers how to run a local Gitea server, **create your admin user**, push your existing local Git repositories (`tekton-catalog` and `dotnet-tekton-sample`) to it, and configure the Webhook so Tekton Pipelines-as-Code (PaC) can trigger automatically.

---

## 📋 Prerequisites
*   Docker and Docker Compose installed.
*   Your local repositories (`tekton-catalog` and `dotnet-tekton-sample`) initialized with Git.
*   PaC port-forward running in a separate terminal:
    ```bash
    kubectl port-forward --address 0.0.0.0 svc/pipelines-as-code-controller 8080:8080 -n pipelines-as-code
    ```

---

## 🐳 Step 1: Run Gitea via Docker Compose

Create a folder for Gitea and add a `docker-compose.yml` file.

```yaml
networks:
  gitea:
    external: false

services:
  server:
    image: docker.gitea.com/gitea:28.3.0
    container_name: gitea
    environment:
      - USER_UID=1000
      - USER_GID=1000
    restart: always
    networks:
      - gitea
    volumes:
      - ./gitea:/data
      - /etc/timezone:/etc/timezone:ro
      - /etc/localtime:/etc/localtime:ro
    ports:
      - "3000:3000"
      - "222:22"
```

Start it:
```bash
docker compose up -d
```

---

## 👤 Step 2: Initial Setup & Create the Admin User

1.  Open your browser and go to **`http://localhost:3000`**.
2.  You will see the initial configuration page. 
3.  Scroll down to the **Administrator Account Settings** section. This is **crucial**—you must create this user now, as you will use it to push your code and manage the repositories.
    *   **Administrator Username:** `admin` (or your preferred name)
    *   **Password:** Choose a secure password.
    *   **Confirm Password:** Re-enter it.
    *   **Email:** `admin@example.com` (or any valid format).
4.  Leave the database settings as **SQLite3** (the default for local development).
5.  Click **Install Gitea**.
6.  Once installed, log in using the admin credentials you just created.

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
    *Repeat these exact steps for your `dotnet-tekton-sample` folder.* The application repository must include `.tekton/push.yaml` on its `main` branch before webhook testing.

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

## ⚙️ Step 5: Configure Gitea Egress Policy (Crucial!)

By default, Gitea blocks webhooks from hitting private IP addresses (like your local k3d cluster). You must allow it.

1.  Open the Gitea configuration file on your host machine: `./gitea/gitea/conf/app.ini`.
2.  Add or update this section at the bottom:
    ```ini
    [webhook]
    ALLOWED_HOST_LIST = host.docker.internal, private
    ```
3.  Restart Gitea:
    ```bash
    docker compose restart server
    ```

---

## 🔗 Step 6: Configure the Webhook

1.  In Gitea, go to your **`dotnet-tekton-sample`** repository.
2.  Click **Settings** → **Webhooks** → **Add Webhook** (Gitea).
3.  Fill in the fields exactly as follows:
    *   **Target URL:** `http://host.docker.internal:8080`
    *   **HTTP Method:** `POST`
    *   **POST Content Type:** `application/json`
    *   **Secret:** `my-super-secret-webhook`
    *   **Trigger On:** `Push Events`
4.  Click **Add Webhook**.
5.  **Test it:** Click on the webhook you just created, scroll down, and click **Test Delivery**. You should get a green `202 Accepted` response.

---

## ✅ Step 7: Final Check (The Repository CR)

Make sure your Kubernetes `Repository` CR (`repo.yaml`) matches the exact URL Gitea uses for the repository. 

```bash
kubectl get repo -n default -o yaml
```
Ensure the `spec.url` is set to:
`http://localhost:3000/<admin>/dotnet-tekton-sample`

*(PaC uses `http://host.docker.internal:3000` to talk to the Gitea API, but the `spec.url` must match the exact string Gitea sends in the webhook payload).*

Once this is done, pushing code to the `dotnet-tekton-sample` repository will automatically trigger Tekton!