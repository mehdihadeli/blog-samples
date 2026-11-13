import io
import json
from pathlib import Path, PurePosixPath
import sys
import tempfile
import time
import urllib.request
import uuid
import xml.etree.ElementTree as ET
import zipfile

from provision import GITEA, GITEA_AUTH, SETTINGS, STATE, git, gitlab


EVIDENCE = Path("/evidence")
MANIFEST = json.loads((STATE / "manifest.json").read_text())
PROJECT = MANIFEST["gitlab_project_id"]


def unpack(content):
    with zipfile.ZipFile(io.BytesIO(content)) as archive:
        for name in archive.namelist():
            path = PurePosixPath(name)
            if path.is_absolute() or ".." in path.parts:
                raise RuntimeError("Invalid artifact path")
        archive.extractall(EVIDENCE)


def show_failed_jobs(pipeline_id):
    jobs = gitlab.request("GET", f"/projects/{PROJECT}/pipelines/{pipeline_id}/jobs?per_page=100")
    for job in jobs:
        if job["status"] == "failed":
            trace = gitlab.request("GET", f"/projects/{PROJECT}/jobs/{job['id']}/trace", raw=True).decode(errors="replace")
            print(f"Failed job {job['name']}:\n" + "\n".join(trace.splitlines()[-45:]), flush=True)


def simulate():
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    project = gitlab.request("GET", f"/projects/{PROJECT}")
    config_url = MANIFEST["ci_config_url"]
    if project["ci_config_path"] != config_url:
        raise RuntimeError("GitLab is not configured to load pipeline YAML from Gitea")
    lint = gitlab.request("POST", f"/projects/{PROJECT}/ci/lint", {
        "content": "include:\n  - remote: " + config_url + "\n"
    })
    resolved_includes = {item["location"] for item in lint.get("includes", [])}
    if not lint["valid"] or not set(MANIFEST["ci_template_urls"]).issubset(resolved_includes):
        raise RuntimeError("GitLab did not resolve the shared pipeline and job templates")
    previous = gitlab.request("GET", f"/projects/{PROJECT}/pipelines?per_page=1")
    baseline = previous[0]["id"] if previous else 0
    with tempfile.TemporaryDirectory() as temporary:
        directory = Path(temporary)
        git(directory, "clone", "--branch", MANIFEST["branch"],
            f"{GITEA}/{MANIFEST['gitea_repository']}.git", ".", authorization=GITEA_AUTH)
        git(directory, "config", "user.name", "Local CI Simulation")
        git(directory, "config", "user.email", "simulation@example.test")
        marker = {"simulation": str(uuid.uuid4()), "description": "Real Gitea push triggers local GitLab CI"}
        (directory / "simulation.json").write_text(json.dumps(marker, indent=2) + "\n")
        git(directory, "add", "simulation.json")
        git(directory, "commit", "-m", "Verify Gitea push triggers GitLab Docker pipeline")
        revision = git(directory, "rev-parse", "HEAD")
        git(directory, "push", "origin", MANIFEST["branch"], authorization=GITEA_AUTH)
    print(f"Pushed Gitea commit {revision}; waiting for webhook-created pipeline.", flush=True)

    deadline = time.monotonic() + 1200
    pipeline = None
    last_status = None
    while time.monotonic() < deadline:
        if pipeline is None:
            candidates = gitlab.request("GET", f"/projects/{PROJECT}/pipelines?source=trigger&per_page=20")
            candidates = [candidate for candidate in candidates if candidate["id"] > baseline]
            for candidate in candidates:
                jobs = gitlab.request("GET", f"/projects/{PROJECT}/pipelines/{candidate['id']}/jobs?per_page=100")
                fetch = next((job for job in jobs if job["name"] == "fetch-source"), None)
                if fetch and fetch["status"] == "success":
                    source = gitlab.request("GET", f"/projects/{PROJECT}/jobs/{fetch['id']}/artifacts/source.json")
                    if source["revision"] == revision:
                        pipeline = candidate
                        break
                elif candidate["status"] in {"failed", "canceled"}:
                    show_failed_jobs(candidate["id"])
                    raise RuntimeError(f"Webhook pipeline {candidate['id']} failed before source verification")
        if pipeline:
            pipeline = gitlab.request("GET", f"/projects/{PROJECT}/pipelines/{pipeline['id']}")
            if pipeline["status"] != last_status:
                print(f"Pipeline {pipeline['id']}: {pipeline['status']}", flush=True)
                last_status = pipeline["status"]
            if pipeline["status"] == "success":
                break
            if pipeline["status"] in {"failed", "canceled", "skipped"}:
                show_failed_jobs(pipeline["id"])
                raise RuntimeError(f"Pipeline ended with {pipeline['status']}")
        time.sleep(3)
    else:
        raise TimeoutError("No successful webhook pipeline within 20 minutes; inspect Gitea webhook deliveries and Runner logs")

    jobs = gitlab.request("GET", f"/projects/{PROJECT}/pipelines/{pipeline['id']}/jobs?per_page=100")
    expected_jobs = {"fetch-source", "build", "test", "publish"}
    if {job["name"] for job in jobs if job["status"] == "success"} != expected_jobs:
        raise RuntimeError("Expected four successful pipeline jobs")
    legacy_yaml = gitlab.request("GET", f"/projects/{PROJECT}/repository/files/.gitlab-ci.yml?ref={pipeline['sha']}", missing_ok=True)
    if legacy_yaml is not None:
        raise RuntimeError("GitLab orchestration commit still contains pipeline YAML")
    fetch = next(job for job in jobs if job["name"] == "fetch-source")
    trace = gitlab.request("GET", f"/projects/{PROJECT}/jobs/{fetch['id']}/trace", raw=True).decode(errors="replace")
    if f"pipeline YAML loaded from {config_url}" not in trace:
        raise RuntimeError("Fetch job did not report the expected external CI configuration URL")
    for name in ["test", "publish"]:
        job = next(job for job in jobs if job["name"] == name)
        content = gitlab.request("GET", f"/projects/{PROJECT}/jobs/{job['id']}/artifacts", raw=True)
        (EVIDENCE / f"{name}-artifacts.zip").write_bytes(content)
        unpack(content)
    source = json.loads((EVIDENCE / "source.json").read_text())
    if source["revision"] != revision or source["repository"] != MANIFEST["gitea_repository"]:
        raise RuntimeError("Published artifact source does not match pushed Gitea commit")
    counters = ET.parse(EVIDENCE / "TestResults/tests.trx").find(".//{*}Counters")
    if counters is None or any(int(counters.get(key, "-1")) != value for key, value in
                               {"total": 3, "executed": 3, "passed": 3, "failed": 0}.items()):
        raise RuntimeError("Expected exactly three passing tests in TRX report")
    if not (EVIDENCE / "publish/Sample.Api.dll").is_file():
        raise RuntimeError("Published application assembly missing")
    result = {
        "pipeline_id": pipeline["id"], "pipeline_url": pipeline["web_url"],
        "pipeline_source": pipeline["source"], "pipeline_status": pipeline["status"],
        "gitlab_project_revision": pipeline["sha"], "gitea_revision": revision,
        "ci_config_url": config_url, "gitlab_repository_contains_ci_yaml": False,
        "gitea_templates_repository": MANIFEST["gitea_templates_repository"],
        "ci_template_urls": MANIFEST["ci_template_urls"],
        "gitea_repository": MANIFEST["gitea_repository"], "tests_passed": 3,
        "jobs": [{"name": job["name"], "status": job["status"], "id": job["id"]} for job in jobs]
    }
    if result["pipeline_source"] != "trigger":
        raise RuntimeError("Pipeline was not triggered through webhook endpoint")
    (EVIDENCE / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2), flush=True)


def check_runtime():
    responses = {}
    for endpoint in ["health", "weatherforecast"]:
        with urllib.request.urlopen(f"http://sample-app:8080/{endpoint}", timeout=15) as response:
            responses[endpoint] = json.load(response)
    if responses["health"] != {"status": "healthy"}:
        raise RuntimeError("Published application health response incorrect")
    forecasts = responses["weatherforecast"]
    if [forecast["temperatureF"] for forecast in forecasts] != [23, 32, 50, 68, 86]:
        raise RuntimeError("Published application forecast response incorrect")
    (EVIDENCE / "runtime.json").write_text(json.dumps(responses, indent=2) + "\n")
    print("Published application HTTP checks passed: /health and /weatherforecast.", flush=True)


if __name__ == "__main__":
    if "--check-runtime" in sys.argv:
        check_runtime()
    else:
        simulate()