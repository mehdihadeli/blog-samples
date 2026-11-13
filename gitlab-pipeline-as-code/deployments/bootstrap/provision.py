import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request


ROOT = Path("/sample")
STATE = Path("/state")
SETTINGS = json.loads((ROOT / "deployments/bootstrap/settings.json").read_text())
GITLAB = SETTINGS["gitlab"]["url"]
GITEA = SETTINGS["gitea"]["url"]
OWNER = os.environ["GITEA_ADMIN_USERNAME"]
GITLAB_TOKEN = (STATE / "gitlab-api-token").read_text().strip()
GITEA_AUTH = "Basic " + base64.b64encode(
    f"{OWNER}:{os.environ['GITEA_ADMIN_PASSWORD']}".encode()
).decode()


class Api:
    def __init__(self, base, headers):
        self.base = base
        self.headers = headers

    def request(self, method, path, data=None, missing_ok=False, raw=False):
        headers = {**self.headers, "Content-Type": "application/json"}
        body = json.dumps(data).encode() if data is not None else None
        request = urllib.request.Request(self.base + path, body, headers, method=method)
        for attempt in range(6):
            try:
                with urllib.request.urlopen(request, timeout=120) as response:
                    content = response.read()
                    if raw:
                        return content
                    return json.loads(content) if content else None
            except urllib.error.HTTPError as error:
                if missing_ok and error.code == 404:
                    return None
                if method == "GET" and error.code in {429, 502, 503, 504} and attempt < 5:
                    print(f"Transient API HTTP {error.code}; retrying read.", flush=True)
                    time.sleep(2 ** attempt)
                    continue
                raise RuntimeError(f"API {method} {path.split('?')[0]} returned HTTP {error.code}") from None
            except (urllib.error.URLError, TimeoutError):
                if method == "GET" and attempt < 5:
                    time.sleep(2 ** attempt)
                    continue
                raise RuntimeError(f"API {method} {path.split('?')[0]} unavailable") from None


gitlab = Api(GITLAB + "/api/v4", {"PRIVATE-TOKEN": GITLAB_TOKEN})
gitea = Api(GITEA + "/api/v1", {"Authorization": GITEA_AUTH})


def store(name, value):
    path = STATE / name
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n")
    temporary.chmod(0o600)
    temporary.replace(path)


def git(directory, *arguments, authorization=None):
    command = ["git"]
    if authorization:
        command.extend(["-c", f"http.extraHeader=Authorization: {authorization}"])
    command.extend(arguments)
    result = subprocess.run(command, cwd=directory, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(f"Git {arguments[0]} failed: {result.stderr.strip()}")
    return result.stdout.strip()


def seed_repository(source, url, authorization, branch, replacements=None):
    with tempfile.TemporaryDirectory() as temporary:
        directory = Path(temporary)
        if git(directory, "ls-remote", "--heads", url, branch, authorization=authorization):
            print(f"Repository already contains {branch}; source left unchanged.")
            return
        shutil.copytree(source, directory, dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns("bin", "obj", ".git", "TestResults", "publish"))
        for path in directory.rglob("*.yml"):
            content = path.read_text()
            for original, replacement in (replacements or {}).items():
                content = content.replace(original, replacement)
            path.write_text(content)
        git(directory, "init", "-b", branch)
        git(directory, "config", "user.name", "Local CI Bootstrap")
        git(directory, "config", "user.email", "bootstrap@example.test")
        git(directory, "add", ".")
        git(directory, "commit", "-m", "Add sample application" if source.name == "application" else "Add reusable GitLab templates")
        git(directory, "remote", "add", "origin", url)
        git(directory, "push", "-u", "origin", branch, authorization=authorization)


def ensure_gitea_repository(name, description, branch):
    path = f"{OWNER}/{name}"
    repository = gitea.request("GET", f"/repos/{path}", missing_ok=True)
    if repository is None:
        gitea.request("POST", "/user/repos", {
            "name": name, "description": description,
            "private": False, "auto_init": False, "default_branch": branch
        })
    return path


def provision():
    project_path = f"{SETTINGS['gitlab']['namespace']}/{SETTINGS['gitlab']['project']}"
    encoded_path = urllib.parse.quote(project_path, safe="")
    project = gitlab.request("GET", f"/projects/{encoded_path}", missing_ok=True)
    if project is None:
        namespaces = gitlab.request("GET", "/namespaces?search=" + urllib.parse.quote(SETTINGS["gitlab"]["namespace"]))
        namespace = next(item for item in namespaces if item["full_path"] == SETTINGS["gitlab"]["namespace"])
        project = gitlab.request("POST", "/projects", {
            "name": SETTINGS["gitlab"]["project"], "path": SETTINGS["gitlab"]["project"],
            "namespace_id": namespace["id"], "description": SETTINGS["gitlab"]["description"],
            "visibility": "private", "initialize_with_readme": True, "default_branch": "main"
        })
    project_id = project["id"]
    templates_branch = SETTINGS["gitea"]["templates_branch"]
    templates_path = ensure_gitea_repository(
        SETTINGS["gitea"]["templates_repository"], "Reusable GitLab pipelines and job templates", templates_branch)
    templates_url = f"{GITEA}/{templates_path}/raw/branch/{urllib.parse.quote(templates_branch, safe='')}"
    replacements = {"http://gitea:3000/admin/gitlab-templates/raw/branch/main": templates_url}
    seed_repository(ROOT / "gitlab-templates", f"{GITEA}/{templates_path}.git", GITEA_AUTH,
                    templates_branch, replacements)
    branch = SETTINGS["gitea"]["branch"]
    repository_path = ensure_gitea_repository(
        SETTINGS["gitea"]["repository"], SETTINGS["gitea"]["description"], branch)
    seed_repository(ROOT / "application", f"{GITEA}/{repository_path}.git", GITEA_AUTH, branch, replacements)

    encoded_branch = urllib.parse.quote(branch, safe="")
    pipeline_path = f"/repos/{repository_path}/contents/.gitlab-ci.yml"
    application_config = (ROOT / "application/.gitlab-ci.yml").read_text()
    for original, replacement in replacements.items():
        application_config = application_config.replace(original, replacement)
    existing_pipeline = gitea.request("GET", pipeline_path + f"?ref={encoded_branch}", missing_ok=True)
    legacy_hash = "d202cacb2225c6dd6035348cdc50529f3c21c20f112735e5a364f22fc1c86870"
    existing_content = base64.b64decode(existing_pipeline["content"]).decode().replace("\r\n", "\n") if existing_pipeline else ""
    if existing_pipeline is None or hashlib.sha256(existing_content.encode()).hexdigest() == legacy_hash:
        update = {
            "branch": branch, "message": "Reuse pipeline from gitlab-templates repository",
            "content": base64.b64encode(application_config.encode()).decode()
        }
        if existing_pipeline:
            update["sha"] = existing_pipeline["sha"]
        gitea.request("PUT" if existing_pipeline else "POST", pipeline_path, update)
    elif existing_content != application_config:
        print("Existing application pipeline differs from sample; left unchanged. Review shared-template include manually.")
    config_url = f"{GITEA}/{repository_path}/raw/branch/{encoded_branch}/.gitlab-ci.yml"
    lint = gitlab.request("POST", f"/projects/{project_id}/ci/lint", {
        "content": "include:\n  - remote: " + config_url + "\n"
    })
    if not lint["valid"]:
        raise RuntimeError("Gitea pipeline configuration is invalid: " + "; ".join(lint["errors"]))
    gitlab.request("PUT", f"/projects/{project_id}", {
        "shared_runners_enabled": False, "ci_config_path": config_url
    })
    legacy_pipeline = gitlab.request("GET", f"/projects/{project_id}/repository/files/.gitlab-ci.yml?ref=main", missing_ok=True)
    if legacy_pipeline is not None:
        gitlab.request("DELETE", f"/projects/{project_id}/repository/files/.gitlab-ci.yml", {
            "branch": "main", "commit_message": "Use pipeline configuration hosted in Gitea",
            "last_commit_id": legacy_pipeline["last_commit_id"]
        })

    variables = {
        "GITEA_URL": GITEA, "GITEA_REPOSITORY": repository_path,
        "GITEA_BRANCH_REF": "refs/heads/" + SETTINGS["gitea"]["branch"],
        "DOTNET_IMAGE": os.environ["DOTNET_IMAGE"], "PYTHON_IMAGE": os.environ["PYTHON_IMAGE"]
    }
    for key, value in variables.items():
        path = f"/projects/{project_id}/variables/{key}"
        existing = gitlab.request("GET", path, missing_ok=True)
        gitlab.request("PUT" if existing else "POST", path if existing else f"/projects/{project_id}/variables",
                       {"key": key, "value": value, "protected": False, "masked": False})

    trigger_path = STATE / "trigger.json"
    trigger = json.loads(trigger_path.read_text()) if trigger_path.exists() else None
    if trigger is None or gitlab.request("GET", f"/projects/{project_id}/triggers/{trigger['id']}", missing_ok=True) is None:
        trigger = gitlab.request("POST", f"/projects/{project_id}/triggers", {"description": "Gitea push webhook"})
        store("trigger.json", trigger)
    target = f"{GITLAB}/api/v4/projects/{project_id}/ref/main/trigger/pipeline?token={trigger['token']}"
    hooks = gitea.request("GET", f"/repos/{repository_path}/hooks")
    existing_hook = next((hook for hook in hooks if hook["config"].get("url", "").startswith(
        f"{GITLAB}/api/v4/projects/{project_id}/")), None)
    hook_data = {
        "type": "gitea", "active": True, "events": ["push"],
        "branch_filter": SETTINGS["gitea"]["branch"],
        "config": {"url": target, "content_type": "json", "http_method": "POST"}
    }
    if existing_hook:
        hook = gitea.request("PATCH", f"/repos/{repository_path}/hooks/{existing_hook['id']}", hook_data)
    else:
        hook = gitea.request("POST", f"/repos/{repository_path}/hooks", hook_data)

    registration_path = STATE / "runner.json"
    registration = json.loads(registration_path.read_text()) if registration_path.exists() else None
    if registration is None or gitlab.request("GET", f"/runners/{registration['id']}", missing_ok=True) is None:
        registration = gitlab.request("POST", "/user/runners", {
            "runner_type": "project_type", "project_id": project_id,
            "description": SETTINGS["runner"]["description"], "locked": True,
            "run_untagged": False, "tag_list": ",".join(SETTINGS["runner"]["tags"]),
            "access_level": "not_protected", "maximum_timeout": 1800
        })
        store("runner.json", registration)
    gitlab.request("POST", "/runners/verify", {"token": registration["token"]})
    runner_config = Path("/runner/config.toml")
    if not runner_config.exists():
        runner_config.write_text(
            f"concurrent = {SETTINGS['runner']['concurrent']}\ncheck_interval = 3\n\n"
            f"[[runners]]\n  name = {json.dumps(SETTINGS['runner']['description'])}\n"
            f"  url = {json.dumps(GITLAB)}\n  token = {json.dumps(registration['token'])}\n"
            f"  executor = \"docker\"\n  [runners.docker]\n"
            f"    image = {json.dumps(os.environ['DOTNET_IMAGE'])}\n"
            f"    network_mode = {json.dumps(SETTINGS['runner']['network'])}\n"
            "    privileged = false\n    pull_policy = \"if-not-present\"\n"
            "    volumes = [\"/cache\"]\n"
        )
        runner_config.chmod(0o600)
    manifest = {
        "gitlab_project_id": project_id, "gitlab_project_path": project_path,
        "ci_config_url": config_url,
        "gitea_templates_repository": templates_path,
        "ci_template_urls": [f"{templates_url}/{path}" for path in [
            "pipelines/dotnet-ci.yml", "templates/gitea-source.yml", "templates/dotnet-base.yml",
            "templates/dotnet-build.yml", "templates/dotnet-test.yml", "templates/dotnet-publish.yml"]],
        "gitea_repository": repository_path, "gitea_hook_id": hook["id"],
        "runner_id": registration["id"], "branch": SETTINGS["gitea"]["branch"]
    }
    store("manifest.json", manifest)
    print(f"Provisioned Gitea {repository_path} and {templates_path}, GitLab {project_path}, and Docker Runner {registration['id']}.")


if __name__ == "__main__":
    provision()