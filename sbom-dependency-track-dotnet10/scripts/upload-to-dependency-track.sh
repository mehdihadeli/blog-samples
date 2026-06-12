#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SBOM_PATH="${ROOT_DIR}/artifacts/sbom/sbom.cyclonedx.json"

DT_BASE_URL="${DT_BASE_URL:-http://localhost:8081}"
DT_API_KEY="${DT_API_KEY:-}"
DT_PROJECT_UUID="${DT_PROJECT_UUID:-}"

if [[ -z "${DT_API_KEY}" ]]; then
  echo "DT_API_KEY is required."
  exit 1
fi

if [[ -z "${DT_PROJECT_UUID}" ]]; then
  echo "DT_PROJECT_UUID is required."
  exit 1
fi

if [[ ! -f "${SBOM_PATH}" ]]; then
  echo "SBOM file not found at ${SBOM_PATH}. Run scripts/generate-sbom.sh first."
  exit 1
fi

echo "Uploading ${SBOM_PATH} to ${DT_BASE_URL}/api/v1/bom"

curl -sS -X POST "${DT_BASE_URL}/api/v1/bom" \
  -H "X-API-Key: ${DT_API_KEY}" \
  -H "Content-Type: multipart/form-data" \
  -F "project=${DT_PROJECT_UUID}" \
  -F "bom=@${SBOM_PATH}"

echo
echo "Upload request sent. Check Dependency-Track project dashboard for processing results."