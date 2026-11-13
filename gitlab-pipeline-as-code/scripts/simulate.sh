#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

export MSYS_NO_PATHCONV=1
docker compose --profile verify run --rm --no-deps simulate
docker compose --profile verify up -d --force-recreate --wait --wait-timeout 120 sample-app
docker compose --profile verify run --rm --no-deps simulate \
    python /sample/deployments/bootstrap/simulate.py --check-runtime
printf '\nPublished application: http://localhost:%s/weatherforecast\n' "${APP_HTTP_PORT:-5080}"