"""Cross-instance Forgejo/Gitea webhook bridge.

Neither Forgejo nor Gitea can trigger a workflow on a *different* instance:
there is no `repository_dispatch` endpoint, and the only remote trigger
(`POST /repos/{owner}/{repo}/actions/workflows/{workflow_id}/dispatches`)
requires a JSON body of `{"ref": "..."}` that a push webhook does not send.

This bridge receives the push webhook delivered by the *source* instance and
calls `workflow_dispatch` on the *target* instance. Standard library only.
"""

from __future__ import annotations

import json
import os
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib import error, request

GITEA_URL = os.environ.get("GITEA_URL", "http://gitea-app:3000").rstrip("/")
TARGET_REPO = os.environ.get("TARGET_REPO", "admin/dotnet-ci")
TARGET_WORKFLOW = os.environ.get("TARGET_WORKFLOW", "ci.yml")
TARGET_REF = os.environ.get("TARGET_REF", "main")
SOURCE_BRANCH = os.environ.get("SOURCE_BRANCH", "main")
TOKEN = os.environ.get("GITEA_TOKEN", "")
PORT = int(os.environ.get("PORT", "8080"))

# Forgejo sends all of these on the same delivery so that GitHub/Gitea/Gogs
# integrations keep working; take the first one that is present.
EVENT_HEADERS = ("X-Forgejo-Event", "X-Gitea-Event", "X-GitHub-Event", "X-Gogs-Event")

DISPATCH_URL = (
    f"{GITEA_URL}/api/v1/repos/{TARGET_REPO}"
    f"/actions/workflows/{TARGET_WORKFLOW}/dispatches"
)


def dispatch_workflow() -> tuple[int, str]:
    """Ask the target instance to run the workflow on TARGET_REF."""
    if not TOKEN:
        return 500, "GITEA_TOKEN is not set"

    body = json.dumps({"ref": TARGET_REF}).encode("utf-8")
    req = request.Request(
        DISPATCH_URL,
        data=body,
        method="POST",
        headers={
            "Authorization": f"token {TOKEN}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )

    try:
        with request.urlopen(req, timeout=15) as response:
            return response.status, ""
    except error.HTTPError as exc:
        detail = exc.read().decode("utf-8", "replace")[:500]
        return exc.code, detail
    except error.URLError as exc:
        return 502, str(exc.reason)


def log(message: str) -> None:
    print(f"[bridge] {message}", flush=True)


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _respond(self, status: int, payload: dict) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802 - http.server API
        if self.path.rstrip("/") == "/healthz":
            self._respond(200, {"status": "ok"})
            return
        self._respond(404, {"error": "not found"})

    def do_POST(self) -> None:  # noqa: N802 - http.server API
        if self.path.rstrip("/") not in ("/hook", ""):
            self._respond(404, {"error": "not found"})
            return

        length = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(length) if length else b""
        event = ""
        for header in EVENT_HEADERS:
            value = (self.headers.get(header) or "").strip()
            if value:
                event = value
                break

        # Ignore anything that is not a push to the watched branch.
        if event and event != "push":
            log(f"ignored event {event!r}")
            self._respond(200, {"status": "ignored", "reason": f"event {event!r}"})
            return

        ref = ""
        try:
            ref = json.loads(raw or b"{}").get("ref", "")
        except json.JSONDecodeError:
            pass

        expected = f"refs/heads/{SOURCE_BRANCH}"
        if ref and ref != expected:
            log(f"ignored push to {ref!r} (watching {expected!r})")
            self._respond(200, {"status": "ignored", "reason": f"ref {ref!r}"})
            return

        log(f"push to {ref or '<unknown>'} -> dispatching {TARGET_REPO} {TARGET_WORKFLOW}@{TARGET_REF}")
        status, detail = dispatch_workflow()
        if 200 <= status < 300:
            log(f"dispatched (HTTP {status})")
            self._respond(202, {"status": "dispatched", "ref": TARGET_REF})
            return

        log(f"dispatch failed (HTTP {status}): {detail}")
        self._respond(502, {"status": "failed", "http": status, "detail": detail})

    def log_message(self, fmt: str, *args) -> None:  # keep logs tidy
        log(fmt % args)


def main() -> int:
    if not TOKEN:
        log("WARNING: GITEA_TOKEN is empty; /hook will answer 502")
    log(f"listening on :{PORT}")
    log(f"target: {DISPATCH_URL} ref={TARGET_REF} source branch={SOURCE_BRANCH}")
    ThreadingHTTPServer(("", PORT), Handler).serve_forever()
    return 0


if __name__ == "__main__":
    sys.exit(main())
