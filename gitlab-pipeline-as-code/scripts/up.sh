#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

export MSYS_NO_PATHCONV=1
docker compose config --quiet
docker compose up -d --wait --wait-timeout 1200 gitlab
docker compose up -d --force-recreate --always-recreate-deps --wait --wait-timeout 120 gitea
docker compose exec -T gitlab install -d -o git -g git -m 0700 /bootstrap-state
docker compose exec -T gitlab gitlab-rails runner /bootstrap/seed.rb
docker compose --profile setup build bootstrap
docker compose --profile setup run --rm --no-deps bootstrap
docker compose up -d runner
printf '\nGitea: http://localhost:%s\nGitLab: http://localhost:%s\n' "${GITEA_HTTP_PORT:-3000}" "${GITLAB_HTTP_PORT:-8929}"