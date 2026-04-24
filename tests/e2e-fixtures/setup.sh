#!/bin/bash
set -euo pipefail

APP_NAME="${1:?Usage: setup.sh <AppName> <AppFolder>}"
APP_FOLDER="${2:?Usage: setup.sh <AppName> <AppFolder>}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

ENTITIES_DIR="$APP_FOLDER/Backend/$APP_NAME.Business/Entities"
INFRA_DIR="$APP_FOLDER/Backend/$APP_NAME.Infrastructure"
E2E_DIR="$APP_FOLDER/Frontend/e2e"
APP_PAGES_DIR="$APP_FOLDER/Frontend/src/app/pages"

echo "=== Spiderly E2E Fixtures Setup ==="
echo "App name: $APP_NAME"
echo "App folder: $APP_FOLDER"

# --- Copy entity files ---
echo "Copying entity files..."
cp "$SCRIPT_DIR/backend/entities/Product.cs" "$ENTITIES_DIR/Product.cs"
cp "$SCRIPT_DIR/backend/entities/User.cs" "$ENTITIES_DIR/User.cs"
cp "$SCRIPT_DIR/backend/entities/TaskCategory.cs" "$ENTITIES_DIR/TaskCategory.cs"
cp "$SCRIPT_DIR/backend/entities/Project.cs" "$ENTITIES_DIR/Project.cs"
cp "$SCRIPT_DIR/backend/entities/ProjectMember.cs" "$ENTITIES_DIR/ProjectMember.cs"
cp "$SCRIPT_DIR/backend/entities/ProjectTask.cs" "$ENTITIES_DIR/ProjectTask.cs"
cp "$SCRIPT_DIR/backend/entities/TaskComment.cs" "$ENTITIES_DIR/TaskComment.cs"

# --- Copy ApplicationDbContext ---
echo "Copying ApplicationDbContext..."
cp "$SCRIPT_DIR/backend/infrastructure/ApplicationDbContext.cs" "$INFRA_DIR/${APP_NAME}ApplicationDbContext.cs"

# --- Replace __APP_NAME__ placeholder in copied .cs files ---
echo "Replacing __APP_NAME__ with $APP_NAME in copied files..."
find "$ENTITIES_DIR" "$INFRA_DIR" -name "*.cs" -exec sed -i "s/__APP_NAME__/$APP_NAME/g" {} +

# --- Copy E2E test helpers ---
echo "Copying E2E test helpers..."
mkdir -p "$E2E_DIR/helpers"
cp "$SCRIPT_DIR/frontend/tests/e2e/helpers/"*.ts "$E2E_DIR/helpers/"

# --- Copy E2E test specs ---
echo "Copying E2E test specs..."
mkdir -p "$E2E_DIR/specs"
cp "$SCRIPT_DIR/frontend/tests/e2e/specs/"*.spec.ts "$E2E_DIR/specs/"

# --- Copy page objects (overwrites base-page.ts) ---
echo "Copying page objects..."
mkdir -p "$E2E_DIR/page-objects"
cp "$SCRIPT_DIR/frontend/tests/e2e/page-objects/"*.ts "$E2E_DIR/page-objects/"

# --- Copy E2E binary fixtures ---
echo "Copying E2E fixtures..."
mkdir -p "$E2E_DIR/fixtures"
cp -r "$SCRIPT_DIR/frontend/tests/e2e/fixtures/." "$E2E_DIR/fixtures/"

# --- Copy Angular list component overrides ---
# Spiderly-cli generates minimal list components (Id column only). Tests that need
# varied filter types replace the generated file with a richer version.
echo "Copying Angular list component overrides..."
cp "$SCRIPT_DIR/frontend/app/product/product-list.component.ts" "$APP_PAGES_DIR/product/product-list.component.ts"

echo "=== Fixtures setup complete ==="
