#!/bin/sh
# Single entry point for the framework-metadata SSOT regen pipeline (see docs/framework-metadata-ssot.md).
# Callers: .githooks/pre-commit (Debug) and .github/workflows/ci.yml (Release).
# Usage: tools/regen-metadata.sh [BuildConfiguration]
set -e
cd "$(dirname "$0")/.."

if [ ! -d tools/node_modules ]; then
  (cd tools && npm ci)
fi

dotnet run --project Spiderly.MetadataExporter -c "${1:-Debug}" -- --out framework-metadata.json
node tools/extract-ts-metadata.mjs
node tools/gen-skill-docs.mjs
