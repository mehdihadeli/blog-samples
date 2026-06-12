#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${ROOT_DIR}/artifacts/sbom"
OUTPUT_FILE="sbom.cyclonedx.json"
SOLUTION_FILE="${ROOT_DIR}/SbomDependencyTrackDemo.slnx"

mkdir -p "${OUTPUT_DIR}"

pushd "${ROOT_DIR}" >/dev/null

dotnet restore "${SOLUTION_FILE}"
dotnet tool restore

# Generate a CycloneDX JSON SBOM from the solution.
dotnet dotnet-CycloneDX "${SOLUTION_FILE}" \
  -o "${OUTPUT_DIR}" \
  -fn "${OUTPUT_FILE}" \
  -F Json \
  -rs

popd >/dev/null

echo "SBOM generated at ${OUTPUT_DIR}/${OUTPUT_FILE}"