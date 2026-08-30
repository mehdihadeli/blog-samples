import base64
import json
import os
import re
import time
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def request(method, url, payload=None):
    body = json.dumps(payload).encode() if payload is not None else None
    username = os.environ["GF_SECURITY_ADMIN_USER"]
    password = os.environ["GF_SECURITY_ADMIN_PASSWORD"]
    credentials = base64.b64encode(f"{username}:{password}".encode()).decode()
    headers = {
        "Authorization": f"Basic {credentials}",
        "Content-Type": "application/json",
    }
    return urlopen(Request(url, data=body, headers=headers, method=method), timeout=10)


base_url = os.environ.get("GRAFANA_URL", "http://grafana:3000")
for _ in range(30):
    try:
        request("GET", f"{base_url}/api/health")
        break
    except (HTTPError, URLError):
        time.sleep(2)
else:
    raise RuntimeError("Grafana did not become ready")

for dashboard_path in sorted(Path("/dashboards").glob("*.json")):
    with dashboard_path.open(encoding="utf-8") as dashboard_file:
        dashboard = json.load(dashboard_file)

    # Import a database-managed copy without changing the source JSON file.
    dashboard["id"] = None
    dashboard["uid"] = re.sub(
        r"[^A-Za-z0-9_-]", "-", dashboard_path.stem
    )[:40].strip("-")

    payload = {
        "dashboard": dashboard,
        "folderId": 0,
        "overwrite": True,
        "inputs": [
            {
                "name": "DS_PROMETHEUS",
                "type": "datasource",
                "pluginId": "prometheus",
                "value": "Prometheus",
            },
            {
                "name": "ds_prometheus",
                "type": "datasource",
                "pluginId": "prometheus",
                "value": "Prometheus",
            },
        ],
    }
    try:
        response = request("POST", f"{base_url}/api/dashboards/import", payload)
        print(f"Imported {dashboard_path.name}: {response.read().decode()}")
    except HTTPError as error:
        print(f"Failed to import {dashboard_path.name}: {error.read().decode()}")
        raise
